using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight.RabbitTrick
{
    [Serializable] public class TrickHand { public Vector2 position; public HandItem holding; public bool gripping; public int custom = -1; }
    public sealed class DualHandController : MonoBehaviour
    {
        public float speed = 2.2f, walkSpeed = 2.2f, grabRadius = 0.34f;
        public float rabbitGrabRadius = 0.25f;  // 离兔子外形还差多远也算抓到（按外形算，见 DistanceToRabbit）
        public float upperArm = 0.53f, forearm = 0.53f;
        public float Reach => upperArm + forearm - 0.01f;
        public int Active { get; private set; }
        public Vector2 BodyPosition { get; private set; }
        public TrickHand[] Hands { get; } = new[] { new TrickHand(), new TrickHand() };
        public TrickHand Selected => Hands[Active];
        public TrickHand Other => Hands[1 - Active];
        public Vector2 Shoulder(int index) => ShoulderAt(BodyPosition, index);
        static Vector2 ShoulderAt(Vector2 body, int index) => body + new Vector2(index == 0 ? -0.27f : 0.27f, 1.35f);
        public Vector2 Elbow(int index)
        {
            Vector2 start = Shoulder(index), delta = Hands[index].position - start;
            float distance = Mathf.Clamp(delta.magnitude, 0.02f, Reach);
            Vector2 axis = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
            float along = (upperArm * upperArm - forearm * forearm + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperArm * upperArm - along * along));
            Vector2 perpendicular = new Vector2(-axis.y, axis.x);
            if (Vector2.Dot(perpendicular, new Vector2(index == 0 ? -0.5f : 0.5f, -0.8f)) < 0) perpendicular = -perpendicular;
            return start + axis * along + perpendicular * height;
        }
        public void ResetHands()
        {
            Active = 0; BodyPosition = new Vector2(3.85f, 1.18f); rabbitGrip = Vector2.down * 0.12f;
            for (int i = 0; i < 2; i++) { Hands[i].position = BodyPosition + new Vector2(i == 0 ? -0.62f : 0.62f, 0.85f); Hands[i].holding = HandItem.None; Hands[i].custom = -1; }
        }
        public void Tick(RabbitTrickGame game, float dt)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.fKey.wasPressedThisFrame) Active = 1 - Active;
            var walk = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            var hand = new Vector2((kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0), (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0));
            MoveBody(game, walk.normalized * walkSpeed * dt);
            Move(game, hand.normalized * speed * dt);
            // 按住 E 抓，松开 E 放。按住期间按 F 切手，原来那只手会一直抓着（交接就靠这个）。
            if (kb.eKey.wasPressedThisFrame) Grab(game);
            if (kb.eKey.wasReleasedThisFrame) Release(game);
            // 握拳的画面：当前这只手只要按着 E 就握拳（哪怕没抓到东西），另一只手抓着东西时保持握拳。
            for (int i = 0; i < 2; i++)
                Hands[i].gripping = Hands[i].holding != HandItem.None || (i == Active && kb.eKey.isPressed);
            game.Audience.DancerDragging = Hands[0].holding == HandItem.Dancer || Hands[1].holding == HandItem.Dancer;
        }
        public void MoveBody(RabbitTrickGame game, Vector2 delta)
        {
            var target = BodyPosition + new Vector2(delta.x, delta.y * 0.55f);
            target.x = Mathf.Clamp(target.x, 0.85f, 9.15f); target.y = Mathf.Clamp(target.y, 1.18f, 1.72f);
            delta = target - BodyPosition;
            if (delta.sqrMagnitude < 0.000001f) return;
            for (int i = 0; i < 2; i++)
            {
                var item = Hands[i].holding; Vector2 anchor = Hands[i].position;
                if (item == HandItem.Custom)
                {
                    // 第 2、3 关：抓着固定把手时身体走不远；拿着能搬动的道具就跟着走
                    if (game.Trick != null && game.Trick.Anchor(Hands[i], out var fixedAt) && Vector2.Distance(ShoulderAt(target, i), fixedAt) > Reach) return;
                    continue;
                }
                if (item == HandItem.Box || item == HandItem.Dancer) anchor += Vector2.right * delta.x;
                if (item != HandItem.None && item != HandItem.Rabbit && Vector2.Distance(ShoulderAt(target, i), anchor) > Reach) return;
            }
            BodyPosition = target;
            for (int i = 0; i < 2; i++)
            {
                var hand = Hands[i];
                if (hand.holding == HandItem.Box) { game.SetBoxX(game.BoxX + delta.x); hand.position = game.BoxHandle; }
                else if (hand.holding == HandItem.Dancer) { game.Audience.SetDancerX(game.Audience.DancerX + delta.x); hand.position = game.DancerHandle; }
                else if (hand.holding == HandItem.Lid) hand.position = game.LidHandle;
                else if (hand.holding == HandItem.Custom && game.Trick != null && game.Trick.Anchor(hand, out var fixedAt)) hand.position = fixedAt;
                else hand.position = ClampHand(i, hand.position + delta);
            }
            UpdateRabbit(game);
        }
        public void Move(RabbitTrickGame game, Vector2 delta)
        {
            var hand = Selected;
            if (hand.holding == HandItem.Lid)
            {
                float amount = Mathf.Clamp01(game.LidAmount + delta.y / 0.7f);
                if (InReach(Active, game.LidHandleAt(amount))) game.SetLid(amount);
                hand.position = game.LidHandle;
            }
            else if (hand.holding == HandItem.Box)
            {
                float oldX = game.BoxX;
                if (InReach(Active, game.BoxHandle + Vector2.right * delta.x))
                {
                    game.SetBoxX(game.BoxX + delta.x);
                    for (int i = 0; i < 2; i++) if (Hands[i].holding != HandItem.None && !InReach(i, Hands[i].position)) game.SetBoxX(oldX);
                }
                hand.position = game.BoxHandle;
            }
            else if (hand.holding == HandItem.Dancer)
            {
                if (game.Audience.Dancing)
                {
                    if (InReach(Active, game.DancerHandle + Vector2.right * delta.x)) game.Audience.SetDancerX(game.Audience.DancerX + delta.x);
                    hand.position = game.DancerHandle;
                }
                else
                {
                    hand.position = ClampHand(Active, hand.position + delta);
                    if (hand.position.y > game.DancerHandle.y + 0.22f) { hand.holding = HandItem.None; game.StartDancing(); }
                }
            }
            else if (hand.holding == HandItem.Custom && game.Trick != null) game.Trick.MoveHeld(hand, Active, delta);
            else hand.position = ClampHand(Active, hand.position + delta);
            UpdateRabbit(game);
        }
        public Vector2 ClampHand(int index, Vector2 position)
        {
            Vector2 delta = position - Shoulder(index);
            if (delta.magnitude < 0.12f) delta = (delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down) * 0.12f;
            return Shoulder(index) + Vector2.ClampMagnitude(delta, Reach);
        }
        bool InReach(int index, Vector2 position) => Vector2.Distance(Shoulder(index), position) <= Reach;
        // 抓住兔子时记下手和兔子的相对位置，拿着走的时候保持不变，兔子不会一下跳到手心。
        Vector2 rabbitGrip = Vector2.down * 0.12f;
        void UpdateRabbit(RabbitTrickGame game) { foreach (var hand in Hands) if (hand.holding == HandItem.Rabbit) game.RabbitPosition = hand.position + rabbitGrip; }
        // 按一下切换抓 / 放（编辑器里的自动验证用）；玩家实际操作走 Grab / Release。
        public void Interact(RabbitTrickGame game)
        {
            if (Selected.holding != HandItem.None) Release(game);
            else Grab(game);
        }

        public void Release(RabbitTrickGame game)
        {
            var hand = Selected;
            if (hand.holding == HandItem.Custom)
            {
                game.Trick?.Release(hand, Active);
                hand.holding = HandItem.None;
                hand.custom = -1;
                return;
            }
            if (hand.holding == HandItem.Rabbit)
            {
                bool atBox = Vector2.Distance(game.RabbitPosition, game.BoxOpening) < 0.5f;
                if (atBox && game.TryPutRabbit()) { hand.holding = HandItem.None; return; }
                // 松开 E 就一定松手：没放进箱子就掉回地上
                hand.holding = HandItem.None;
                game.DropRabbit();
                return;
            }
            hand.holding = HandItem.None;
        }

        public void Grab(RabbitTrickGame game)
        {
            var hand = Selected;
            if (hand.holding != HandItem.None) return;
            if (game.Trick != null)
            {
                if (!game.Trick.TryGrab(hand, Active)) game.Notify("Walk closer with WASD, move the hand onto a handle or a prop with the ARROWS, then hold E.");
                return;
            }
            if (Other.holding == HandItem.Rabbit && Vector2.Distance(hand.position, game.RabbitPosition) <= rabbitGrabRadius + 0.1f)
            {
                Other.holding = HandItem.None; hand.holding = HandItem.Rabbit;
                rabbitGrip = Vector2.ClampMagnitude(game.RabbitPosition - hand.position, 0.6f); UpdateRabbit(game);
                game.Notify("Rabbit passed to your " + (Active == 0 ? "LEFT" : "RIGHT") + " hand. Keep holding E."); return;
            }
            HandItem nearest = HandItem.None; float distance = float.MaxValue;
            Check(HandItem.Lid, game.LidHandle, ref nearest, ref distance);
            Check(HandItem.Box, game.BoxHandle, ref nearest, ref distance);
            if (!game.Rules.RabbitInside && Other.holding != HandItem.Rabbit)
            {
                // 兔子按整个身体（含耳朵）算：手碰到兔子身上任意一处就能抓，不要求够到兔子的中心。
                float d = DistanceToRabbit(hand.position, game.RabbitPosition);
                if (d <= rabbitGrabRadius && d < distance) { distance = d; nearest = HandItem.Rabbit; }
            }
            Check(HandItem.Dancer, game.DancerHandle, ref nearest, ref distance);
            if (nearest != HandItem.None && Other.holding == nearest) nearest = HandItem.None;
            hand.holding = nearest;
            if (nearest == HandItem.Rabbit) { rabbitGrip = game.RabbitPosition - hand.position; game.Rules.TouchRabbit(); UpdateRabbit(game); }
            else if (nearest == HandItem.Lid) hand.position = game.LidHandle;
            else if (nearest == HandItem.Box) hand.position = game.BoxHandle;
            else if (nearest == HandItem.Dancer) hand.position = game.DancerHandle;
            else game.Notify("Walk closer with WASD, move the hand onto a handle with the ARROWS, then hold E.");
        }
        // 兔子的外形范围（相对 RabbitPosition）：身体到耳朵尖。
        static readonly Rect RabbitBounds = new Rect(-0.3f, -0.3f, 0.6f, 0.98f);
        static float DistanceToRabbit(Vector2 hand, Vector2 rabbit)
        {
            Vector2 local = hand - rabbit;
            float dx = Mathf.Max(RabbitBounds.xMin - local.x, 0f, local.x - RabbitBounds.xMax);
            float dy = Mathf.Max(RabbitBounds.yMin - local.y, 0f, local.y - RabbitBounds.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        void Check(HandItem item, Vector2 point, ref HandItem nearest, ref float distance) => Check(item, point, ref nearest, ref distance, grabRadius);
        void Check(HandItem item, Vector2 point, ref HandItem nearest, ref float distance, float radius)
        {
            float candidate = Vector2.Distance(Selected.position, point);
            if (candidate <= radius && candidate < distance && InReach(Active, point)) { distance = candidate; nearest = item; }
        }
        public void SelectHand(int index) => Active = Mathf.Clamp(index, 0, 1);
    }
}
