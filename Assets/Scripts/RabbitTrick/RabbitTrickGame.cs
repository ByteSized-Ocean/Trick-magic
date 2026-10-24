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
            Hands.ResetHands();
            Audience.ResetLight(BoxX);
            if (view != null) view.Draw(this);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) ResetTrick();
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Rules.Failed || Rules.Finished)
            {
                ResultAge += dt;
                if (Rules.Finished) Audience.Tick(dt);
            }
            else
            {
                Hands.Tick(this, dt);
                Audience.Tick(dt);
                Evaluate(dt);
            }
            if ((Rules.Failed || Rules.Finished) && !resultSoundPlayed)
            {
                resultSoundPlayed = true;
                if (audioSource != null) audioSource.PlayOneShot(Rules.Failed ? bell : reveal, 0.45f);
            }
            view.Draw(this);
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
            if (Rules.Step == TrickStep.ShowEmpty) { Notify("先让观众看清空箱子，再藏兔子"); return false; }
            if (LidAmount < 0.92f) { Notify("箱盖还没完全打开：抓住箱盖，向上拉"); return false; }
            bool inserted = Rules.TryInsert(LidAmount >= 0.92f,
                Audience.RabbitVisibleInLight(RabbitPosition), Audience.Illuminates(BoxOpening, 0.24f) && !Audience.CoversRabbit(BoxOpening));
            if (inserted) Notify("兔子已藏好！趁暗抓住箱盖向下拉，关紧箱子");
            return inserted;
        }

        public void DropRabbit()
        {
            // Releasing in the light must be checked before the rabbit returns to the floor.
            Rules.CheckExposure(Audience.RabbitVisibleInLight(RabbitPosition), false);
            RabbitPosition = new Vector2(RabbitPosition.x, 1.5f);
        }

        public void StartDancing()
        {
            Audience.Distract(Audience.DancerX);
            Notify("小人开始跳舞！可以抓着侧面拉环移动，用裙摆遮住兔子穿过亮处");
        }

        public void Notify(string message)
        {
            notification = message;
            notificationUntil = Time.unscaledTime + 3f;
        }

        public string Hint(bool chinese)
        {
            if (Rules.Failed || Rules.Finished) return "";
            if (chinese && Time.unscaledTime < notificationUntil) return notification;
            if (Hands.Selected.holding == HandItem.Lid)
                return chinese ? "抓住了箱盖：↑ 拉开 / ↓ 合上，E 松手" : "HOLDING LID: UP opens / DOWN closes. E releases.";
            if (Hands.Selected.holding == HandItem.Box)
                return chinese ? "抓住了箱体：← → 拖动箱子，E 松手" : "HOLDING BOX: LEFT / RIGHT to drag. E releases.";
            if (Hands.Selected.holding == HandItem.Dancer)
                return Audience.Dancing ? (chinese ? "左右拖动小人：把兔子藏在裙摆正后方，离开遮挡就会露馅" : "Drag the decoy. Keep the whole rabbit behind its cape in the light.")
                    : (chinese ? "抓住了小人的拉环：向上拉，让它跳舞" : "Pull UP to wind the dancing decoy.");
            if (Rules.Step == TrickStep.ShowEmpty)
                return chinese ? "WASD 走近箱子；方向键把手移到箱盖，E 抓住后向上拉，必须在灯下展示" : "WASD walk to box. ARROWS move hand. E grabs lid; pull UP in the LIGHT.";
            if (Rules.Step == TrickStep.HideRabbit)
            {
                if (Rules.RabbitInside) return chinese ? "兔子已经在里面了：趁暗抓住箱盖，向下拉到完全关闭" : "RABBIT INSIDE: pull the lid DOWN completely while it is dark.";
                if (Hands.Selected.holding == HandItem.Rabbit)
                    return chinese ? "保持兔子在暗处，或藏在移动小人的裙摆后；到箱口按 E 放入。F 切手可交接" : "Carry in DARKNESS or behind the moving cape. E inserts at the box. F switches hand.";
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
