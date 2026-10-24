using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sleight.RabbitTrick
{
    // 每关开始前的演示：魔术师按正确解法自己演一遍，底部配字幕，玩家按任意键跳过。
    // 不是录好的视频，而是一串脚本动作直接驱动游戏本身（走位、换手、抓放、追光），
    // 所以演示里的判定和真正玩的时候完全一样；编辑器里还能快进跑一遍验证每关都演得通。
    public sealed class TrickDemo
    {
        readonly List<Func<float, bool>> steps = new List<Func<float, bool>>();
        readonly RabbitTrickGame game;
        int index;
        float stepTime;
        const float StepTimeout = 8f;

        public string Caption { get; private set; } = "";
        public bool Done => index >= steps.Count;
        public float WalkSpeed = 2.2f, HandSpeed = 2.2f;

        public TrickDemo(RabbitTrickGame g) { game = g; }

        public void Tick(float dt)
        {
            if (Done) return;
            for (int i = 0; i < 2; i++)
                game.Hands.Hands[i].gripping = game.Hands.Hands[i].holding != HandItem.None;
            stepTime += dt;
            // 某一步卡住（够不着之类）就跳过，演示不会停在半路
            if (steps[index](dt) || stepTime > StepTimeout) { index++; stepTime = 0f; }
        }

        // ------------------------------------------------------------ 脚本动作

        public TrickDemo Say(string text) { steps.Add(_ => { Caption = text; return true; }); return this; }

        public TrickDemo Wait(float seconds)
        {
            float t = 0f;
            steps.Add(dt => (t += dt) >= seconds);
            return this;
        }

        public TrickDemo Walk(float x)
        {
            float stuck = 0f;
            steps.Add(dt =>
            {
                var body = game.Hands.BodyPosition;
                float dx = x - body.x;
                if (Mathf.Abs(dx) < 0.02f) return true;
                game.Hands.MoveBody(game, Vector2.right * Mathf.Clamp(dx, -WalkSpeed * dt, WalkSpeed * dt));
                stuck = Mathf.Abs(game.Hands.BodyPosition.x - body.x) < 0.0001f ? stuck + dt : 0f;
                return stuck > 0.3f;
            });
            return this;
        }

        public TrickDemo Hand(int i) { steps.Add(_ => { game.Hands.SelectHand(i); return true; }); return this; }

        public TrickDemo HandTo(Func<Vector2> target)
        {
            float stuck = 0f;
            steps.Add(dt =>
            {
                var hand = game.Hands.Selected.position;
                var delta = target() - hand;
                if (delta.magnitude < 0.03f) return true;
                game.Hands.Move(game, Vector2.ClampMagnitude(delta, HandSpeed * dt));
                stuck = (game.Hands.Selected.position - hand).sqrMagnitude < 1e-8f ? stuck + dt : 0f;
                return stuck > 0.3f;
            });
            return this;
        }

        // 抓着东西往某个方向拉（开箱盖、拉帘子、推拉杆）
        public TrickDemo HandBy(Vector2 total)
        {
            float moved = 0f;
            steps.Add(dt =>
            {
                float step = Mathf.Min(HandSpeed * dt, total.magnitude - moved);
                game.Hands.Move(game, total.normalized * step);
                moved += step;
                return moved >= total.magnitude - 0.0001f;
            });
            return this;
        }

        public TrickDemo Grab() { steps.Add(_ => { game.Hands.Grab(game); return true; }); return this; }
        public TrickDemo Release() { steps.Add(_ => { game.Hands.Release(game); return true; }); return this; }

        public TrickDemo Light(float x, float duration)
        {
            bool started = false;
            steps.Add(_ =>
            {
                if (!started) { game.Audience.ScriptMove(x, duration); started = true; }
                return !game.Audience.Moving;
            });
            return this;
        }

        public TrickDemo Until(Func<bool> condition) { steps.Add(_ => condition()); return this; }

        // ------------------------------------------------------------ 第 1 关：空箱变兔子

        public static TrickDemo Rabbit(RabbitTrickGame g)
        {
            var d = new TrickDemo(g);
            return d
                .Say("1  While the LIGHT is on the box, open the lid and show it is empty.")
                .Walk(5.1f).Hand(1).HandTo(() => g.LidHandle).Grab().HandBy(Vector2.up * 0.8f)
                .Until(() => g.Rules.Step != TrickStep.ShowEmpty).Release()
                .Say("2  When the light moves away, carry the rabbit to the box in the DARK.")
                .Light(8.6f, 1.2f)
                .Walk(1.55f).Hand(0).HandTo(() => g.RabbitPosition + Vector2.up * 0.2f).Grab()
                .Walk(5.25f).HandTo(() => g.BoxOpening + (g.Hands.Selected.position - g.RabbitPosition) + Vector2.up * 0.1f).Release()
                .Say("Still in the dark: pull the lid DOWN to close the box.")
                .Hand(1).HandTo(() => g.LidHandle).Grab().HandBy(Vector2.down * 0.8f).Release()
                .Say("3  When the light comes back, open the box: a rabbit!")
                .Light(5.85f, 1.2f)
                .HandTo(() => g.LidHandle).Grab().HandBy(Vector2.up * 0.8f)
                .Wait(0.5f);
        }
    }
}
