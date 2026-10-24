using System;
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
        public StageMenu Menu;
        public StageSoundtrack Soundtrack;
        public StageCurtains Curtains;
        public float curtainDelay = 2.2f;   // 成功后先让观众看清，再落幕
        public float closedHold = 1.0f;     // 幕合拢后停多久再拉开
        // 三关，难度逐级递增。第 1 关是原来的空箱变兔子（Trick == null），第 2、3 关各是一个 StageTrick。
        public const int LevelCount = 5;
        readonly StageTrick[] tricks = { null, new TrunkTrick(), new LevitationTrick(), new DoveHatTrick(), new DepthTransferTrick() };
        static readonly float[] PatienceByLevel = { 65f, 70f, 100f, 100f, 120f };
        float rabbitLookTime;
        bool rabbitShown, rabbitLookAtDancer;
        // 追光偏向舞台某一段：(机会, 区间左, 区间右, 最短停留, 最长停留)。
        // 第 2 关偏左，给足暗处换人的时间；第 3 关偏中间，悬浮桌在 x 5.2，让她多被照到
        static readonly float[][] BiasByLevel = { new float[5], new[] { 0.6f, 1.4f, 3.0f, 3.5f, 5.5f }, new[] { 0.7f, 4.6f, 5.8f, 4.5f, 7f } };
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
        public float DemoAge { get; private set; }
        public const float DemoSkipLock = 3f;   // 演示开始后 3 秒内按键不跳过，免得还没看清就跳过了
        public bool CanSkipDemo => InDemo && DemoAge >= DemoSkipLock;
        public bool LastDemoSucceeded { get; private set; }   // 编辑器验证用
        public string LastDemoReport { get; private set; } = "";
        public static bool SimulateInEditor;                  // 编辑器里快进跑演示时为 true
        TrickDemo demo;
        public RabbitTrickRules Rules { get; } = new RabbitTrickRules();
        public float BoxX { get; private set; }
        public float LidAmount { get; private set; }
        public Vector2 RabbitPosition { get; set; }
        public float RabbitDepth { get; set; }
        public bool RabbitBack { get => RabbitDepth > 0.5f; set => RabbitDepth = value ? 1f : 0f; }
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
        const float Gravity = 9.8f;
        float RabbitFloorY => 1.5f + RabbitDepth * DualHandController.LaneLift;
        string notification;
        float notificationUntil;
        bool resultSoundPlayed;
        AudioClip bell, reveal;

        void Start()
        {
            view.UseRuntimeFont();
            bell = Tone("Caught bell", 660f, 0.32f, false);
            reveal = Tone("Reveal", 440f, 0.45f, true);
            Soundtrack = GetComponent<StageSoundtrack>();
            if (Soundtrack == null) Soundtrack = gameObject.AddComponent<StageSoundtrack>();
            Soundtrack.Initialize();
            Menu = GetComponent<StageMenu>();
            if (Menu == null) Menu = gameObject.AddComponent<StageMenu>();
            Menu.Initialize(this);
            ResetTrick();
            Menu.ShowHome();
        }

        public void StartDemo() => StartDemo(false);

        // looping = true：演示演完一遍又从头开始，跳过锁不再重新计时
        void StartDemo(bool looping)
        {
            ResetTrick();
            InDemo = true;
            if (!looping) DemoAge = 0f;
            Audience.Scripted = Level < 2;
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
        // 左上角的"重播演示"按钮：重新开始本关并播一遍演示
        public void ReplayDemo()
        {
            BeginLevel(Level);
            StartDemo();
        }
        public void BeginLevel(int index)
        {
            StopAllCoroutines(); ChangingLevel = InDemo = false; demo = null;
            Audience.Scripted = false;
            if (Curtains != null) Curtains.SnapOpen();
            Menu?.Hide(); Soundtrack?.StopEffects();
            LoadLevel(index);
        }
        public void RestartLevel() => BeginLevel(Level);
        public void CancelForHome()
        {
            StopAllCoroutines(); ChangingLevel = InDemo = false; demo = null;
            Audience.Scripted = false; Soundtrack?.StopEffects();
            if (Curtains != null) Curtains.SnapOpen();
            ResetTrick();
        }

        void TickDemo(float dt)
        {
            // A domain reload preserves InDemo but cannot serialize the delegate-based demo timeline.
            if (demo == null) { StartDemo(); return; }
            DemoAge += dt;
            if (!IsFailed && !IsFinished)
            {
                demo.Tick(dt);
                DirectAudience(dt);
                Audience.Tick(dt);
                if (Trick != null) Trick.Tick(dt);
                else { TickFalling(dt); Evaluate(dt); }
                if (demo.Done) FinishDemoRound();
            }
            else
            {
                ResultAge += dt;
                if (ResultAge > 1.6f) FinishDemoRound();
            }
        }

        // 演示演完一遍：玩家没按键跳过就从头再放（编辑器验证时直接结束）
        void FinishDemoRound()
        {
            if (SimulateInEditor) { EndDemo(); return; }
            LastDemoSucceeded = IsFinished && !IsFailed;
            StartDemo(true);
        }

        public void ResetTrick()
        {
            Rules.Reset();
            rabbitLookTime = 0f; rabbitShown = rabbitLookAtDancer = false;
            BoxX = 5.85f;
            LidAmount = 0f;
            RabbitPosition = new Vector2(1.3f, 1.5f);
            RabbitBack = false;
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
            Audience.SetBeam(Level == 2 ? 1.95f : 1.45f, Level == 2 ? 0.5f : 0.24f);
            Audience.SetPace(pace.x, pace.y, pace.z, pace.w);
            var bias = BiasByLevel[Mathf.Clamp(Level, 0, BiasByLevel.Length - 1)];
            Audience.SetBias(bias[0], bias[1], bias[2], bias[3], bias[4]);
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
            Audience.ResetLight(Level == 0 ? BoxX : Level == 1 ? Trick.StartLightX : Hands.BodyPosition.x);
            if (view != null) { view.SetLevel(Level); view.Draw(this); }
            Soundtrack?.PlayLevel(Level);
        }

        void Update()
        {
            if (Menu != null && Menu.BlocksGameplay) return;
            var kb = Keyboard.current;
            if (InDemo)
            {
                // 任意键跳过演示
                if (CanSkipDemo && kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) EndDemo();
                else TickDemo(Mathf.Min(Time.deltaTime, 0.05f));
                view.Draw(this);
                return;
            }
            // Restart is available only from the P menu, by click or its R shortcut.
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
                DirectAudience(dt);
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
                if (IsFinished) Soundtrack?.PlayApplause();
            }
            view.Draw(this);
        }

        // Every normal-play light target is a real performer or prop; no empty-space random sweeps.
        public void DirectAudience(float dt)
        {
            if (Trick != null) { Trick.DirectAudience(dt); return; }
            if (Rules.Step == TrickStep.ShowEmpty)
            {
                Audience.Follow(BoxX, 6f, "EMPTY BOX");
                return;
            }
            if (!rabbitShown) { rabbitShown = true; rabbitLookAtDancer = true; rabbitLookTime = 0f; }
            rabbitLookTime += dt;
            if (rabbitLookTime >= (rabbitLookAtDancer ? 8f : 4f))
            {
                rabbitLookTime = 0f;
                rabbitLookAtDancer = !rabbitLookAtDancer;
            }
            Audience.Follow(rabbitLookAtDancer ? 8.25f : BoxX, 6f, rabbitLookAtDancer ? "JUMPING DECOY" : "MAGIC BOX");
        }

        // 过关：两侧大幕合拢 → 停一下 → 在幕后换到下一关 → 拉开。
        IEnumerator NextLevel()
        {
            ChangingLevel = true;
            Curtains.Close();
            yield return new WaitUntil(() => Curtains.Settled);
            yield return new WaitForSeconds(closedHold);
            if (Level + 1 >= LevelCount && Menu != null) { Menu.ShowHome(); yield break; }
            LoadLevel((Level + 1) % LevelCount);
            Curtains.Open();
            yield return new WaitUntil(() => Curtains.Settled);
            ChangingLevel = false;
            // Demonstrations are an explicit action in the P menu.
        }

        void LoadLevel(int index)
        {
            Level = Mathf.Clamp(index, 0, LevelCount - 1);
            ResetTrick();
        }

        // Debug 跳关：立刻换到第 index 关（0 起），不走落幕
        public void JumpToLevel(int index)
        {
            BeginLevel(index);
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
            bool rabbitLit = Rules.RabbitInside ? Audience.RabbitVisibleInLight(BoxOpening)
                : !RabbitBack && Audience.RabbitVisibleInLight(RabbitPosition);   // 后侧的兔子观众看不见
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
            if (RabbitBack) { Notify("Come to the FRONT of the stage (S) to reach the box."); return false; }
            if (LidAmount < 0.92f) { Notify("The lid is not fully open: grab it and pull UP."); return false; }
            bool inserted = Rules.TryInsert(LidAmount >= 0.92f,
                Audience.RabbitVisibleInLight(RabbitPosition), Audience.Illuminates(BoxOpening, 0.24f) && !Audience.CoversRabbit(BoxOpening));
            if (inserted) { RabbitPosition = BoxOpening; RabbitDepth = 0; Notify("Rabbit hidden! While it is dark, pull the lid DOWN to close the box."); }
            return inserted;
        }

        public void DropRabbit()
        {
            // Releasing in the light must be checked before the rabbit returns to the floor.
            Rules.CheckExposure(!RabbitBack && Audience.RabbitVisibleInLight(RabbitPosition), false);
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
            if (Hands.InBack && Hands.Selected.holding == HandItem.None)
                return "BACK OF STAGE. Hold S to move smoothly forward; front handles need you nearby in depth.";
            if (Hands.Selected.holding == HandItem.Lid)
                return "HOLDING LID: UP opens / DOWN closes. Let go of E to release.";
            if (Hands.Selected.holding == HandItem.Box)
                return "HOLDING BOX: LEFT / RIGHT to drag. Let go of E to release.";
            if (Hands.Selected.holding == HandItem.Dancer)
                return Audience.Dancing ? "Drag the decoy. Keep the whole rabbit behind its cape in the light."
                    : "Pull UP to wind the dancing decoy.";
            if (Rules.Step == TrickStep.ShowEmpty)
                return "WASD: walk to the box. ARROWS: move the hand. Hold E on the lid and pull UP in the LIGHT.";
            if (Rules.Step == TrickStep.HideRabbit)
            {
                if (Rules.RabbitInside) return "RABBIT INSIDE: pull the lid DOWN completely while it is dark.";
                if (Hands.Selected.holding == HandItem.Rabbit)
                    return "Keep it in the DARK or at the BACK of the stage. Let go of E at the box. Hold E and press F to pass it.";
                return "Wait for the light to move away, or carry the rabbit along the BACK of the stage (W).";
            }
            return "Wait for the light on the box, then open the lid to reveal the rabbit.";
        }

        // 掌声：几百下短促的噪声"啪"，随机分布在几秒里，开头密、结尾稀
        static AudioClip Applause(float seconds)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * seconds)];
            var rng = new System.Random(11);
            int claps = 420;
            for (int c = 0; c < claps; c++)
            {
                float t = (float)Math.Pow(rng.NextDouble(), 1.6) * (seconds - 0.05f);
                int start = (int)(t * rate);
                float amp = 0.08f + 0.12f * (float)rng.NextDouble();
                int len = (int)(rate * (0.008f + 0.01f * (float)rng.NextDouble()));
                for (int i = 0; i < len && start + i < samples.Length; i++)
                    samples[start + i] += ((float)rng.NextDouble() * 2f - 1f) * amp * Mathf.Exp(-i / (len * 0.35f));
            }
            for (int i = 0; i < samples.Length; i++)
            {
                float fade = Mathf.Clamp01((samples.Length - i) / (rate * 0.6f));
                samples[i] = Mathf.Clamp(samples[i], -0.9f, 0.9f) * fade;
            }
            var clip = AudioClip.Create("Applause", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
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
