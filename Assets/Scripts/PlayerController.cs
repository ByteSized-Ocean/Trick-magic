using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight
{
    public enum PlayerState { Idle, Walk, Interact, Perform, Caught }

    // GDD §7 / §8：移动、交互、呈现、输入缓冲。由 ActRunner 每帧第 1 步调用 Tick（§17.2）。
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 2.0f;
        public float interactRadius = 0.7f;
        public float minX = 0.4f;
        public float maxX = 9.6f;
        public float interactBuffer = 0.10f;
        public float performBuffer = 0.15f;
        public float performDuration = 1.0f;

        public Spotlight spotlight;

        public PlayerState State { get; private set; }

        public event Action PerformStarted;
        public event Action PerformCompleted;
        public event Action<PropInteractable> InteractCompleted;
        public event Action Blocked;   // 光下按键：灰闪 + 闷响，不算失败

        IReadOnlyList<PropInteractable> props = Array.Empty<PropInteractable>();
        float interactPressedAt = float.NegativeInfinity;
        float performPressedAt = float.NegativeInfinity;
        float actionEnd;
        PropInteractable actionTarget;

        public float X => transform.position.x;
        public Zone CurrentZone => Stage.ZoneOf(X);

        // §8.2 "可动手/可起手"的唯一来源：追光落定 且 玩家所在半区 != 被照亮的半区。
        public bool CanAct() => spotlight != null && spotlight.IsSettled() && CurrentZone != spotlight.CurrentZone();
        public bool CanPerform() => CanAct();
        public bool IsOnAssist(Zone z) => Mathf.Abs(X - Stage.AssistX(z)) <= 0.5f;

        public PlayerSnapshot Snapshot(Zone target) => new PlayerSnapshot
        {
            zone = CurrentZone,
            assistDistance = Mathf.Abs(X - Stage.AssistX(target)),
        };

        public void ResetTo(float x, IReadOnlyList<PropInteractable> actProps)
        {
            props = actProps;
            SetX(x);
            State = PlayerState.Idle;
            interactPressedAt = float.NegativeInfinity;
            performPressedAt = float.NegativeInfinity;
            actionTarget = null;
        }

        public void SetCaught() => State = PlayerState.Caught;

        public void Tick(float dt, float now)
        {
            if (State == PlayerState.Caught) return;

            // TODO: 完成时刻改为以 FrameAnim 播完为准（§18.7），现在先用幕时间占位。
            if (State == PlayerState.Interact || State == PlayerState.Perform)
            {
                if (now >= actionEnd) CompleteAction();
                return;
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            float dir = 0f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir += 1f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir -= 1f;
            SetX(X + dir * moveSpeed * dt);
            State = dir != 0f ? PlayerState.Walk : PlayerState.Idle;

            bool ePressed = kb.eKey.wasPressedThisFrame;
            bool spacePressed = kb.spaceKey.wasPressedThisFrame;
            if (ePressed) interactPressedAt = now;
            if (spacePressed) performPressedAt = now;
            if ((ePressed || spacePressed) && !CanAct()) Blocked?.Invoke();

            if (now - performPressedAt <= performBuffer && CanPerform())
            {
                performPressedAt = float.NegativeInfinity;
                State = PlayerState.Perform;
                actionEnd = now + performDuration;
                PerformStarted?.Invoke();
                return;
            }

            if (now - interactPressedAt <= interactBuffer && CanAct())
            {
                var prop = NearestProp();
                if (prop != null)
                {
                    interactPressedAt = float.NegativeInfinity;
                    State = PlayerState.Interact;
                    actionTarget = prop;
                    actionEnd = now + prop.InteractDuration;
                }
            }
        }

        void CompleteAction()
        {
            var finished = State;
            State = PlayerState.Idle;
            if (finished == PlayerState.Perform)
            {
                PerformCompleted?.Invoke();
            }
            else if (actionTarget != null)
            {
                var target = actionTarget;
                actionTarget = null;
                InteractCompleted?.Invoke(target);
            }
        }

        PropInteractable NearestProp()
        {
            PropInteractable best = null;
            float bestDist = interactRadius;
            foreach (var p in props)
            {
                float d = Mathf.Abs(p.X - X);
                if (d <= bestDist)
                {
                    best = p;
                    bestDist = d;
                }
            }
            return best;
        }

        void SetX(float x)
        {
            var pos = transform.position;
            transform.position = new Vector3(Mathf.Clamp(x, minX, maxX), pos.y, pos.z);
        }
    }
}
