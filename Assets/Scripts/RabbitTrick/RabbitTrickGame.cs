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
        public const int LevelCount = 1;    // 目前只有一关；以后加关只改这里、LoadLevel 和 TrickNames
        public static readonly string[] TrickNames = { "THE RABBIT FROM AN EMPTY BOX" };
        public string TrickName => TrickNames[Mathf.Clamp(Level, 0, TrickNames.Length - 1)];
        public int Level { get; private set; }
        public float patienceSeconds = 50f;   // 观众的耐心：这么久还没变出兔子，就开始扔东西
        public float Patience { get; private set; } = 1f;   // 1 = 满，0 = 耗尽
        public bool Booed { get; private set; }             // 耐心耗尽导致的失败（台下扔鸡蛋、白菜）
        public bool ChangingLevel { get; private set; }
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
            ResetTrick();
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
            Hands.ResetHands();
            Audience.ResetLight(BoxX);
            if (view != null) view.Draw(this);
        }

        void Update()
        {
            var kb = Keyboard.current;
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
            else if (Rules.Failed || Rules.Finished)
            {
                ResultAge += dt;
                if (Rules.Finished) Audience.Tick(dt);
                if (Rules.Finished && Curtains != null && ResultAge >= curtainDelay) StartCoroutine(NextLevel());
            }
            else
            {
                Hands.Tick(this, dt);
                TickFalling(dt);
                Audience.Tick(dt);
                Evaluate(dt);
                TickPatience(dt);
            }
            if ((Rules.Failed || Rules.Finished) && !resultSoundPlayed)
            {
                resultSoundPlayed = true;
                if (audioSource != null) audioSource.PlayOneShot(Rules.Failed ? bell : reveal, 0.45f);
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
        }

        void LoadLevel(int index)
        {
            Level = index;
            ResetTrick();   // 目前每一关都是兔子魔术；多关以后在这里按 index 换布置
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
            if (Rules.Failed || Rules.Finished) return;
            Patience = Mathf.Max(0f, Patience - dt / Mathf.Max(1f, patienceSeconds));
            if (Patience > 0f) return;
            Booed = true;
            Rules.Expose("The audience ran out of patience and started throwing things.");
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
            if (Rules.Failed || Rules.Finished) return "";
            if (Time.unscaledTime < notificationUntil) return notification;
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
