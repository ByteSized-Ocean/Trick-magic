using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight.RabbitTrick
{
    public sealed class RabbitTrickGame : MonoBehaviour
    {
        public RabbitStageView view;
        public RandomAudienceLight Audience;
        public DualHandController Hands;
        public AudioSource audioSource;
        public StageCurtains Curtains;
        public float curtainDelay = 2.2f;   // 成功后先让观众看清，再落幕
        public float closedHold = 1.0f;     // 幕合拢后停多久再拉开
        // 三关，难度逐级递增。第 1 关是原来的空箱变兔子（Trick == null），第 2、3 关各是一个 StageTrick。
        public const int LevelCount = 3;
        readonly StageTrick[] tricks = { null, new TrunkTrick(), new LevitationTrick() };
        static readonly float[] PatienceByLevel = { 50f, 45f, 40f };
        // 追光：停留最短 / 最长、移动最短 / 最长（秒），逐关更快
        static readonly Vector4[] PaceByLevel = { new Vector4(2.8f, 4.6f, 1.1f, 1.7f), new Vector4(2.2f, 3.6f, 0.9f, 1.4f), new Vector4(1.8f, 3.0f, 0.7f, 1.1f) };
        public StageTrick Trick => tricks[Mathf.Clamp(Level, 0, tricks.Length - 1)];
        public string TrickName => Trick != null ? Trick.Name : "THE RABBIT FROM AN EMPTY BOX";
        public int Level { get; private set; }
        public float patienceSeconds = 50f;   // 观众的耐心：这么久还没完成魔术，就开始扔东西（每关开始时按 PatienceByLevel 重设）

        // 当前关卡的状态，第 1 关读 Rules，第 2、3 关读 Trick
        public bool IsFailed => Trick != null ? Trick.Failed : Rules.Failed;
        public bool IsFinished => Trick != null ? Trick.Finished : Rules.Finished;
        public string FailureReason => Trick != null ? Trick.FailureReason : Rules.FailureReason;
        public int StepIndex => Trick != null ? Trick.Step : (int)Rules.Step;
        public float StepProgress => Trick != null ? Trick.StepProgress : Rules.EmptyShowProgress;
        public string[] StepNames => Trick != null ? Trick.StepNames : RabbitStepNames;
        public string SuccessTitle => Trick != null ? Trick.SuccessTitle : "A RABBIT FROM AN EMPTY BOX!";
        static readonly string[] RabbitStepNames = { "01  SHOW THE EMPTY BOX", "02  HIDE RABBIT + CLOSE BOX", "03  REVEAL UNDER THE LIGHT" };
        void Expose(string reason) { if (Trick != null) Trick.Expose(reason); else Rules.Expose(reason); }
        public float Patience { get; private set; } = 1f;   // 1 = 满，0 = 耗尽
        public bool Booed { get; private set; }             // 耐心耗尽导致的失败（台下扔鸡蛋、白菜）
        public bool ChangingLevel { get; private set; }

        // 每关开始前的演示（TrickDemo）：任意键跳过，演完自动开始
        public bool InDemo { get; private set; }
        public string DemoCaption => demo != null ? demo.Caption : "";
        public bool LastDemoSucceeded { get; private set; }   // 编辑器验证用
        public string LastDemoReport { get; private set; } = "";
        public static bool SimulateInEditor;                  // 编辑器里快进跑演示时为 true
        TrickDemo demo;
        public RabbitTrickRules Rules { get; } = new RabbitTrickRules();
        public float BoxX { get; private set; }
        public float LidAmount { get; private set; }
        public Vector2 RabbitPosition { get; set; }
        public float ResultAge { get; private set; }
        public Vector2 BoxHandle => new Vector2(BoxX, 1.57f);
        public Vector2 BoxOpening => new Vector2(BoxX, 2.16f);
        public Vector2 LidHandle => LidHandleAt(LidAmount);
        public Vector2 LidHandleAt(float amount)
        {
            float angle = amount * 70f * Mathf.Deg2Rad;
            return new Vector2(BoxX - 0.78f + 0.78f * Mathf.Cos(angle) - 0.06f * Mathf.Sin(angle),
                2.11f + 0.78f * Mathf.Sin(angle) + 0.06f * Mathf.Cos(angle));
        }
        public Vector2 DancerHandle => new Vector2(Audience.DancerX + 0.72f, 2.45f);
        float rabbitFallVel, lidFallVel;
        bool rabbitFalling;
        const float Gravity = 9.8f, RabbitFloorY = 1.5f;
        string notification;
        float notificationUntil;
        bool resultSoundPlayed;
        AudioClip bell, reveal;

        void Start()
        {
            view.UseRuntimeFont();
            bell = Tone("Caught bell", 660f, 0.32f, false);
            reveal = Tone("Reveal", 440f, 0.45f, true);
            StartDemo();
        }

        public void StartDemo()
        {
            ResetTrick();
            InDemo = true;
            Audience.Scripted = true;
            demo = new TrickDemo(this);
            if (Trick != null) Trick.AddDemo(demo);
            else demo = TrickDemo.Rabbit(this);
        }

        public void EndDemo()
        {
            LastDemoSucceeded = IsFinished && !IsFailed;
            LastDemoReport = $"level {Level + 1}: step {StepIndex}/{StepNames.Length}, failed={IsFailed} {FailureReason}, caption='{DemoCaption}'";
            InDemo = false;
            demo = null;
            Audience.Scripted = false;
            ResetTrick();
        }

        // 编辑器验证：把第 level 关的演示用固定步长快进跑完，返回是否演成功
        public bool SimulateDemo(int level, float dt = 1f / 60f, int maxFrames = 60 * 180)
        {
            Level = Mathf.Clamp(level, 0, LevelCount - 1);
            StartDemo();
            for (int i = 0; i < maxFrames && InDemo; i++)
            {
                TickDemo(dt);
                view.Draw(this);
            }
            if (InDemo) EndDemo();
            return LastDemoSucceeded;
        }

        // 演示里的一帧：脚本代替键盘驱动双手，其余判定照常跑；不扣耐心、不落幕。
        void TickDemo(float dt)
        {
            if (!IsFailed && !IsFinished)
            {
                demo.Tick(dt);
                Audience.Tick(dt);
                if (Trick != null) Trick.Tick(dt);
                else { TickFalling(dt); Evaluate(dt); }
                if (demo.Done) EndDemo();
            }
            else
            {
                ResultAge += dt;
                if (ResultAge > 1.6f) EndDemo();
            }
        }

        public void ResetTrick()
        {
            Rules.Reset();
            BoxX = 5.85f;
            LidAmount = 0f;
            RabbitPosition = new Vector2(1.3f, 1.5f);
            ResultAge = 0f;
            notification = "";
            notificationUntil = 0;
            resultSoundPlayed = false;
            rabbitFalling = false;
            rabbitFallVel = lidFallVel = 0f;
            Patience = 1f;
            Booed = false;
            patienceSeconds = PatienceByLevel[Mathf.Clamp(Level, 0, PatienceByLevel.Length - 1)];
            var pace = PaceByLevel[Mathf.Clamp(Level, 0, PaceByLevel.Length - 1)];
            Audience.SetPace(pace.x, pace.y, pace.z, pace.w);
            Hands.ResetHands();
            // 第 2、3 关的道具在运行时才搭；编辑器里重建场景时不搭，免得存进场景、运行时又搭一份
            if (Application.isPlaying || SimulateInEditor)
            {
                for (int i = 0; i < tricks.Length; i++)
                {
                    if (tricks[i] == null) continue;
                    tricks[i].Attach(this);
                    tricks[i].SetVisible(i == Level);
                }
                Trick?.Reset();
            }
            Audience.ResetLight(Trick != null ? Trick.StartLightX : BoxX);
            if (view != null) { view.SetLevel(Level); view.Draw(this); }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (InDemo)
            {
                // 任意键跳过演示
                if (kb != null && kb.anyKey.wasPressedThisFrame) EndDemo();
                else TickDemo(Mathf.Min(Time.deltaTime, 0.05f));
                view.Draw(this);
                return;
            }
            if (kb != null && kb.rKey.wasPressedThisFrame && !ChangingLevel) ResetTrick();
#if UNITY_EDITOR
            // 调试：F9 直接走一遍落幕 → 拉开，不用先把魔术演完
            if (kb != null && kb.f9Key.wasPressedThisFrame && !ChangingLevel && Curtains != null) StartCoroutine(NextLevel());
            // 调试：F8 把耐心降到只剩 3 秒
            if (kb != null && kb.f8Key.wasPressedThisFrame) Patience = Mathf.Min(Patience, 3f / Mathf.Max(1f, patienceSeconds));
#endif
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (ChangingLevel)
            {
                Audience.Tick(dt);
            }
            else if (IsFailed || IsFinished)
            {
                ResultAge += dt;
                if (IsFinished) Audience.Tick(dt);
                if (IsFinished && Curtains != null && ResultAge >= curtainDelay) StartCoroutine(NextLevel());
            }
            else
            {
                Hands.Tick(this, dt);
                if (Trick != null)
                {
                    Audience.Tick(dt);
                    Trick.Tick(dt);
                }
                else
                {
                    TickFalling(dt);
                    Audience.Tick(dt);
                    Evaluate(dt);
                }
                TickPatience(dt);
            }
            if ((IsFailed || IsFinished) && !resultSoundPlayed)
            {
                resultSoundPlayed = true;
                if (audioSource != null) audioSource.PlayOneShot(IsFailed ? bell : reveal, 0.45f);
            }
            view.Draw(this);
        }

        // 过关：两侧大幕合拢 → 停一下 → 在幕后换到下一关 → 拉开。
        IEnumerator NextLevel()
        {
            ChangingLevel = true;
            Curtains.Close();
            yield return new WaitUntil(() => Curtains.Settled);
            yield return new WaitForSeconds(closedHold);
            LoadLevel((Level + 1) % LevelCount);
            Curtains.Open();
            yield return new WaitUntil(() => Curtains.Settled);
            ChangingLevel = false;
            StartDemo();
        }

        void LoadLevel(int index)
        {
            Level = Mathf.Clamp(index, 0, LevelCount - 1);
            ResetTrick();
        }

        // Debug 跳关：立刻换到第 index 关（0 起），不走落幕
        public void JumpToLevel(int index)
        {
            StopAllCoroutines();
            ChangingLevel = false;
            if (Curtains != null) Curtains.SnapOpen();
            LoadLevel(index);
            StartDemo();
        }

        // Debug：当前关直接判成功，走一遍正常的成功 → 落幕 → 下一关
        public void DebugWinLevel()
        {
            if (ChangingLevel || IsFinished) return;
            StartCoroutine(NextLevel());
        }

        public void Evaluate(float dt)
        {
            bool boxLit = Audience.Illuminates(BoxOpening) && !Audience.BlocksBox(BoxOpening);
            Rules.ShowEmpty(dt, LidAmount >= 0.92f, boxLit);
            Rules.CloseBox(LidAmount <= 0.08f);
            if (Rules.TryReveal(LidAmount >= 0.92f, boxLit)) return;
            bool rabbitLit = Audience.RabbitVisibleInLight(Rules.RabbitInside ? BoxOpening : RabbitPosition);
            Rules.CheckExposure(rabbitLit, LidAmount > 0.2f);
        }

        public void SetLid(float amount)
        {
            LidAmount = Mathf.Clamp01(amount);
            Rules.CloseBox(LidAmount <= 0.08f);
        }

        public void SetBoxX(float x)
        {
            BoxX = Mathf.Clamp(x, 2.8f, 7.1f);
            foreach (var hand in Hands.Hands)
                if (hand.holding == HandItem.Lid) hand.position = LidHandle;
        }

        public bool TryPutRabbit()
        {
            if (Rules.Step == TrickStep.ShowEmpty) { Notify("Show the audience the empty box first, then hide the rabbit."); return false; }
            if (LidAmount < 0.92f) { Notify("The lid is not fully open: grab it and pull UP."); return false; }
            bool inserted = Rules.TryInsert(LidAmount >= 0.92f,
                Audience.RabbitVisibleInLight(RabbitPosition), Audience.Illuminates(BoxOpening, 0.24f) && !Audience.CoversRabbit(BoxOpening));
            if (inserted) Notify("Rabbit hidden! While it is dark, pull the lid DOWN to close the box.");
            return inserted;
        }

        public void DropRabbit()
        {
            // Releasing in the light must be checked before the rabbit returns to the floor.
            Rules.CheckExposure(Audience.RabbitVisibleInLight(RabbitPosition), false);
            // 中途松手：兔子从手里受重力掉下去，落地轻轻弹一下（TickFalling）。
            rabbitFalling = RabbitPosition.y > RabbitFloorY + 0.001f;
            rabbitFallVel = 0f;
        }

        void TickPatience(float dt)
        {
            if (IsFailed || IsFinished) return;
            Patience = Mathf.Max(0f, Patience - dt / Mathf.Max(1f, patienceSeconds));
            if (Patience > 0f) return;
            Booed = true;
            Expose("The audience ran out of patience and started throwing things.");
        }

        // 松手后的物体：兔子掉到地上；半开的箱盖没人扶着，会绕铰链落下合上（全开时靠在限位上不动）。
        void TickFalling(float dt)
        {
            bool rabbitHeld = Hands.Hands[0].holding == HandItem.Rabbit || Hands.Hands[1].holding == HandItem.Rabbit;
            if (rabbitHeld || Rules.RabbitInside) rabbitFalling = false;
            if (rabbitFalling)
            {
                rabbitFallVel -= Gravity * dt;
                var p = RabbitPosition + Vector2.up * rabbitFallVel * dt;
                if (p.y <= RabbitFloorY)
                {
                    p.y = RabbitFloorY;
                    if (rabbitFallVel < -1.2f) rabbitFallVel = -rabbitFallVel * 0.28f;   // 落地弹一下
                    else { rabbitFallVel = 0f; rabbitFalling = false; }
                }
                RabbitPosition = p;
            }

            bool lidHeld = Hands.Hands[0].holding == HandItem.Lid || Hands.Hands[1].holding == HandItem.Lid;
            if (!lidHeld && LidAmount > 0f && LidAmount < 0.92f)
            {
                lidFallVel += Gravity * 0.9f * dt;
                float amount = LidAmount - lidFallVel * dt;
                if (amount <= 0f)
                {
                    amount = 0f;
                    lidFallVel = lidFallVel > 1.5f ? -lidFallVel * 0.2f : 0f;   // 盖上时轻轻弹起一点
                }
                SetLid(amount);
            }
            else if (lidHeld || LidAmount <= 0f) lidFallVel = 0f;
        }

        public void StartDancing()
        {
            Audience.Distract(Audience.DancerX);
            Notify("The decoy is dancing! Drag it by its side ring and hide the rabbit behind its cape.");
        }

        public void Notify(string message)
        {
            notification = message;
            notificationUntil = Time.unscaledTime + 3f;
        }

        public string Hint(bool chinese)
        {
            if (IsFailed || IsFinished) return "";
            if (Time.unscaledTime < notificationUntil) return notification;
            if (Trick != null) return Trick.Hint();
            if (Hands.Selected.holding == HandItem.Lid)
                return chinese ? "抓着箱盖：↑ 拉开 / ↓ 合上，松开 E 放手" : "HOLDING LID: UP opens / DOWN closes. Let go of E to release.";
            if (Hands.Selected.holding == HandItem.Box)
                return chinese ? "抓着箱体：← → 拖动箱子，松开 E 放手" : "HOLDING BOX: LEFT / RIGHT to drag. Let go of E to release.";
            if (Hands.Selected.holding == HandItem.Dancer)
                return Audience.Dancing ? (chinese ? "左右拖动小人：把兔子藏在裙摆正后方，离开遮挡就会露馅" : "Drag the decoy. Keep the whole rabbit behind its cape in the light.")
                    : (chinese ? "抓住了小人的拉环：向上拉，让它跳舞" : "Pull UP to wind the dancing decoy.");
            if (Rules.Step == TrickStep.ShowEmpty)
                return chinese ? "WASD 走近箱子；方向键把手移到箱盖，按住 E 抓住向上拉，必须在灯下展示" : "WASD: walk to the box. ARROWS: move the hand. Hold E on the lid and pull UP in the LIGHT.";
            if (Rules.Step == TrickStep.HideRabbit)
            {
                if (Rules.RabbitInside) return chinese ? "兔子已经在里面了：趁暗抓住箱盖，向下拉到完全关闭" : "RABBIT INSIDE: pull the lid DOWN completely while it is dark.";
                if (Hands.Selected.holding == HandItem.Rabbit)
                    return chinese ? "保持兔子在暗处，或藏在移动小人的裙摆后；到箱口松开 E 放入。按住 E 时按 F 切手，可以交接" : "Carry it in the DARK or behind the moving cape. Let go of E at the box. Hold E and press F to pass it.";
                return chinese ? "向上拉小人的侧面拉环吸引目光；也能移动小人，让兔子躲在裙摆后通过灯光" : "Wind the decoy. Its moving cape can cover a rabbit passing through the spotlight.";
            }
            return chinese ? "把小人移开：箱口必须在灯下且无遮挡，拉开箱盖给观众看兔子" : "Move the decoy aside. Reveal only when the box is LIT and UNOBSTRUCTED.";
        }

        static AudioClip Tone(string name, float frequency, float seconds, bool rising)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * seconds)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                phase += 2 * Mathf.PI * frequency * (rising ? 1f + t / seconds * 0.5f : 1f) / rate;
                samples[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 12f) * 0.35f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (bell != null) Destroy(bell);
            if (reveal != null) Destroy(reveal);
        }
    }
}
