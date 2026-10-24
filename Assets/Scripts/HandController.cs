using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight
{
    // GDD §0.3：搭台段魔术师那只单独操控的手。方向键移动，按住空格抓、松开放，短按空格翻状态。
    // 手用一根细线连到肩膀，像提线木偶。由 ActRunner 在搭台段每帧调用 Tick。
    public class HandController : MonoBehaviour
    {
        public PlayerController player;
        public LineRenderer line;

        public float speed = 3.0f;
        public float reach = 1.4f;
        public float grabRadius = 0.35f;
        public float tapTime = 0.2f;
        public Vector2 restOffset = new Vector2(0.55f, -0.7f);

        public event Action<PropInteractable> Toggled;
        public event Action<PropInteractable, bool> Dropped;   // bool = 吸附到了槽位

        public PropInteractable Held { get; private set; }

        Vector2 offset;
        float pressedAt = -1f;
        PropInteractable pressTarget;
        IReadOnlyList<PropInteractable> props = Array.Empty<PropInteractable>();
        Func<string, bool> slotFree = _ => true;

        public Vector3 HandPos => player.Shoulder + new Vector3(offset.x, offset.y, -0.05f);

        public void Show(IReadOnlyList<PropInteractable> actProps, Func<string, bool> isSlotFree)
        {
            props = actProps;
            slotFree = isSlotFree;
            offset = restOffset;
            Held = null;
            pressedAt = -1f;
            gameObject.SetActive(true);
            if (line != null) line.enabled = true;
            Sync();
        }

        public void Hide()
        {
            if (Held != null) Release();
            gameObject.SetActive(false);
            if (line != null) line.enabled = false;
        }

        public void Tick(float dt, float now)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var dir = Vector2.zero;
            if (kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.downArrowKey.isPressed) dir.y -= 1f;
            if (kb.rightArrowKey.isPressed) dir.x += 1f;
            if (kb.leftArrowKey.isPressed) dir.x -= 1f;
            offset += dir.normalized * speed * dt;
            offset = Vector2.ClampMagnitude(offset, reach);
            float minY = Stage.Floor + 0.05f - player.Shoulder.y;
            if (offset.y < minY) offset.y = minY;

            if (kb.spaceKey.wasPressedThisFrame)
            {
                pressedAt = now;
                pressTarget = Nearest();
            }
            if (kb.spaceKey.isPressed && Held == null && pressTarget != null && now - pressedAt >= tapTime)
            {
                Held = pressTarget;
                Held.Lift();
            }
            if (kb.spaceKey.wasReleasedThisFrame)
            {
                if (Held != null) Release();
                else if (pressTarget != null && now - pressedAt < tapTime) Toggled?.Invoke(pressTarget);
                pressTarget = null;
            }

            if (Held != null) Held.FollowHand(HandPos);
            Sync();
        }

        void Release()
        {
            var p = Held;
            Held = null;
            bool snapped = p.Drop(slotFree);
            Dropped?.Invoke(p, snapped);
        }

        PropInteractable Nearest()
        {
            PropInteractable best = null;
            float bestSq = grabRadius * grabRadius;
            var hand = HandPos;
            foreach (var p in props)
            {
                var b = p.Bounds;
                var h = new Vector3(hand.x, hand.y, b.center.z);
                float d = b.SqrDistance(h);
                if (d <= bestSq)
                {
                    best = p;
                    bestSq = d;
                }
            }
            return best;
        }

        void Sync()
        {
            transform.position = HandPos;
            if (line != null)
            {
                line.SetPosition(0, player.Shoulder);
                line.SetPosition(1, HandPos);
            }
        }
    }
}
