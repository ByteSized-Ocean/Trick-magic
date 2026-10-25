using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class MirrorBladeTrick : ApparatusTrick
    {
        const int Veil = 0, LeftMirror = 1, RightMirror = 2, Blade = 3;
        Transform cabinet, curtain, whole, halfA, halfB, mirrorA, mirrorB, bladeT, lampA, lampB;
        LineRenderer beamA, beamB;
        float open, left, right, cut, proof, divided, restored;
        public bool Aligned => Mathf.Abs(left - 0.32f) < 0.065f && Mathf.Abs(right - 0.72f) < 0.065f;
        public float BladeTravel => cut;
        public override string Name => "THE MIRROR SPLIT";
        public override string SuccessTitle => "DIVIDED BY LIGHT, RESTORED WHOLE!";
        public override string[] StepNames { get; } = { "01  SHOW THE WHOLE ASSISTANT", "02  ALIGN MIRRORS / PASS THE BLADE", "03  WITHDRAW / RESTORE THE IMAGE" };
        public override string Status => "MIRRORS: " + Mathf.RoundToInt(left * 100) + "% / " + Mathf.RoundToInt(right * 100) + "%   |   ALIGNMENT: " + (Aligned ? "READY" : "TUNE") + "   |   BLADE: " + Mathf.RoundToInt(cut * 100) + "%";
        public override float StartLightX => 6.5f;
        protected override void OnReset() { open = left = right = cut = proof = divided = restored = 0; }
        public override void DirectAudience(float dt) => game.Audience.Follow(6.5f, 8, "MIRROR CABINET");
        public override void Tick(float dt)
        {
            bool watched = Lit(new Vector2(6.5f, 2.5f));
            if (cut > 0.06f && !Aligned && open > 0.2f && watched)
            { Expose("The blade crossed an unprotected image. Align BOTH mirrors before cutting; withdraw the blade before untuning them."); return; }
            if (Step == 0)
            {
                proof = open > 0.9f && left < 0.08f && right < 0.08f && cut < 0.05f && watched ? proof + dt : 0;
                StepProgress = Mathf.Clamp01(proof / 0.8f);
                if (proof >= 0.8f) { Step = 1; StepProgress = 0; }
            }
            else if (Step == 1)
            {
                divided = Aligned && cut > 0.95f && open > 0.9f && watched ? divided + dt : 0;
                StepProgress = Mathf.Clamp01(divided / 0.6f);
                if (divided >= 0.6f) { Step = 2; StepProgress = 0; }
            }
            else if (Step == 2)
            {
                restored = cut < 0.05f && left < 0.08f && right < 0.08f && open > 0.9f && watched ? restored + dt : 0;
                StepProgress = Mathf.Clamp01(restored / 0.8f);
                if (restored >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Open the center curtain UP and show the WHOLE assistant. Leave the mirrors upright for this first proof.";
            if (Step == 1) return Aligned ? "Both lamps are GREEN. Grab the saw's gold grip and drag it RIGHT through the protected image."
                : "Tune the LEFT mirror to 32% and the RIGHT to 72% using UP / DOWN. Green lamps confirm alignment.";
            return "First drag the blade fully LEFT. Then lower BOTH mirror controls to 0% and show her whole again IN THE LIGHT.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Show the assistant whole, with both mirrors upright.")
             .Walk(3.8f).Hand(1).HandTo(() => ControlPoint(Veil)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Step >= 1).Release()
             .Say("2  Two different angles form one convincing image. Tune BOTH mirrors before moving the blade.")
             .Hand(0).HandTo(() => ControlPoint(LeftMirror)).Grab().HandBy(Vector2.up * 0.224f).Release()
             .Hand(1).HandTo(() => ControlPoint(RightMirror)).Grab().HandBy(Vector2.up * 0.504f).Release()
             .HandTo(() => ControlPoint(Blade)).Grab().HandBy(Vector2.right * 1.0f).Until(() => Step >= 2)
             .Say("3  The order matters to the illusion: withdraw the blade, THEN restore the mirrors.")
             .HandBy(Vector2.left * 1.0f).Release()
             .Hand(0).HandTo(() => ControlPoint(LeftMirror)).Grab().HandBy(Vector2.down * 0.7f).Release()
             .Hand(1).HandTo(() => ControlPoint(RightMirror)).Grab().HandBy(Vector2.down * 0.7f).Release().Until(() => Finished);
        }
        protected override void Build()
        {
            IllusionScenery.Build(view, root, 2);
            cabinet = Frame("MirrorCabinet", new Vector2(6.5f, 2.35f), new Vector2(1.85f, 2.2f), Gold);
            curtain = view.Rect("MirrorVeil", root, new Vector2(6.5f, 2.35f), new Vector2(1.8f, 2.15f), Wine, 25).transform;
            whole = Person("WholeAssistant", new Color32(153, 104, 183, 255));
            halfA = view.Group("LeftReflection", root, Vector2.zero); halfB = view.Group("RightReflection", root, Vector2.zero);
            foreach (var half in new[] { halfA, halfB })
            {
                view.Rect("HalfDress", half, new Vector2(0, 0.64f), new Vector2(0.22f, 0.65f), new Color32(153, 104, 183, 255), 20);
                view.Rect("Leg", half, new Vector2(0, 0.2f), new Vector2(0.06f, 0.4f), Cream, 20);
                view.Disc("ReflectedHead", half, new Vector2(0, 1.18f), new Vector2(0.16f, 0.31f), Cream, 21);
            }
            mirrorA = Frame("LeftAngledMirror", new Vector2(5.35f, 2.5f), new Vector2(0.38f, 2.25f), Teal);
            mirrorB = Frame("RightAngledMirror", new Vector2(7.65f, 2.5f), new Vector2(0.38f, 2.25f), Teal);
            beamA = view.Line("LeftReflectedRay", root, 0.025f, Teal, 24, 2);
            beamB = view.Line("RightReflectedRay", root, 0.025f, Teal, 24, 2);
            lampA = view.Disc("LeftAlignmentLamp", root, new Vector2(5.5f, 3.8f), new Vector2(0.13f, 0.13f), Wine, 25).transform;
            lampB = view.Disc("RightAlignmentLamp", root, new Vector2(7.5f, 3.8f), new Vector2(0.13f, 0.13f), Wine, 25).transform;
            bladeT = view.Group("SlidingStageBlade", root, Vector2.zero);
            view.Disc("RoundBlade", bladeT, new Vector2(0, 0.2f), new Vector2(0.4f, 0.4f), new Color32(155, 172, 190, 255), 24);
            view.Rect("BladeArm", bladeT, new Vector2(0, 0.05f), new Vector2(0.08f, 0.4f), Gold, 25);
            view.Rect("MirrorControlDesk", root, new Vector2(3.8f, 1.62f), new Vector2(1.8f, 0.55f), Ink, 18);
            Lever(Veil, new Vector2(4.1f, 2.1f), Vector2.up * 0.35f, Vector2.up, () => open, v => open = v);
            Lever(LeftMirror, new Vector2(3.2f, 2.15f), Vector2.up * 0.4f, Vector2.up, () => left, v => left = v);
            Lever(RightMirror, new Vector2(4.4f, 2.15f), Vector2.up * 0.4f, Vector2.up, () => right, v => right = v);
            Lever(Blade, new Vector2(3.15f, 2.1f), Vector2.right * 1.3f, Vector2.right, () => cut, v => cut = v, 1f);
        }
        public override void Draw()
        {
            StageDepth.Set(cabinet, 0.4f);
            bool split = Aligned && cut > 0.5f;
            whole.gameObject.SetActive(!split); whole.position = new Vector2(6.5f, 1.4f); StageDepth.Set(whole, 0.25f);
            halfA.gameObject.SetActive(split); halfB.gameObject.SetActive(split);
            halfA.position = new Vector2(6.35f - cut * 0.3f, 1.4f); halfB.position = new Vector2(6.65f + cut * 0.3f, 1.4f);
            StageDepth.Set(halfA, 0.25f); StageDepth.Set(halfB, 0.25f);
            Cover(curtain, new Vector2(6.5f, 1.275f), new Vector2(1.8f, 2.15f), 1 - open); StageDepth.Set(curtain, 0.1f);
            mirrorA.localRotation = Quaternion.Euler(0, 0, -left * 16); mirrorB.localRotation = Quaternion.Euler(0, 0, right * 16);
            beamA.SetPosition(0, new Vector2(5.35f, 2.9f)); beamA.SetPosition(1, new Vector2(6.5f, 1.9f + left * 2));
            beamB.SetPosition(0, new Vector2(7.65f, 2.9f)); beamB.SetPosition(1, new Vector2(6.5f, 1.9f + right));
            lampA.GetComponent<SpriteRenderer>().color = Mathf.Abs(left - 0.32f) < 0.065f ? Teal : Wine;
            lampB.GetComponent<SpriteRenderer>().color = Mathf.Abs(right - 0.72f) < 0.065f ? Teal : Wine;
            bladeT.position = new Vector2(5.85f + cut * 1.3f, 2.1f); StageDepth.Set(bladeT, 0.15f);
            DrawControls();
        }
    }
}
