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
        public bool actionsEnabled = true;   // 搭台段关掉：E / 空格交给手（GDD §0.3）

        [Header("帧动画（§18.7，帧图朝右，空着就不播）")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;
        public Sprite[] actFrames;
        public Sprite[] performFrames;
        public Sprite caughtFrame;

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
        FrameAnim anim;
        SpriteRenderer sr;
        PlayerState animState = (PlayerState)(-1);

        void Awake()
        {
            anim = GetComponent<FrameAnim>();
            sr = GetComponent<SpriteRenderer>();
        }

        public float X => transform.position.x;
        public Zone CurrentZone => Stage.ZoneOf(X);

        // §8.2 "可动手/可起手"的唯一来源：追光落定 且 玩家所在半区 != 被照亮的半区。
        public bool CanAct() => spotlight != null && spotlight.IsSettled() && CurrentZone != spotlight.CurrentZone();
        public bool CanPerform() => CanAct();
        public bool IsOnAssist(Zone z) => Mathf.Abs(X - Stage.AssistX(z)) <= 0.5f;

        public Vector3 Shoulder => transform.position + new Vector3(0f, 2.15f, -0.05f);

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
            if (sr != null) sr.flipX = false;
            UpdateAnim(true);
        }

        public void SetCaught()
        {
            State = PlayerState.Caught;
            UpdateAnim(false);
        }

        // 光下按键的灰闪 0.1s（§8.2）。
        public void FlashBlocked()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            StopAllCoroutines();
            StartCoroutine(Flash(sr));
        }

        System.Collections.IEnumerator Flash(SpriteRenderer sr)
        {
            var original = sr.color;
            sr.color = new Color(0.45f, 0.45f, 0.48f, original.a);
            yield return new WaitForSeconds(0.1f);
            sr.color = original;
        }

        public void Tick(float dt, float now)
        {
            if (State == PlayerState.Caught) return;

            // TODO: 完成时刻改为以 FrameAnim 播完为准（§18.7），现在先用幕时间占位。
            if (State == PlayerState.Interact || State == PlayerState.Perform)
            {
                if (now >= actionEnd) CompleteAction();
                UpdateAnim(false);
                return;
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            float dir = 0f;
            // 搭台段方向键归手，只能用 A / D 走（GDD §0.3）。
            if (kb.dKey.isPressed || (actionsEnabled && kb.rightArrowKey.isPressed)) dir += 1f;
            if (kb.aKey.isPressed || (actionsEnabled && kb.leftArrowKey.isPressed)) dir -= 1f;
            SetX(X + dir * moveSpeed * dt);
            State = dir != 0f ? PlayerState.Walk : PlayerState.Idle;
            if (dir != 0f && sr != null) sr.flipX = dir < 0f;

            if (!actionsEnabled)
            {
                UpdateAnim(false);
                return;
            }

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
                UpdateAnim(false);
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
            UpdateAnim(false);
        }

        // 状态变了才换一组帧；动手 / 呈现的帧率按动作时长摊开，最后一帧正好落在完成时刻。
        void UpdateAnim(bool force)
        {
            if (anim == null || (!force && State == animState)) return;
            animState = State;
            switch (State)
            {
                case PlayerState.Walk: Play(walkFrames, 10f, true); break;
                case PlayerState.Interact:
                    Play(actFrames, Frames(actFrames) / (actionTarget != null ? actionTarget.InteractDuration : 0.6f), false);
                    break;
                case PlayerState.Perform: Play(performFrames, Frames(performFrames) / performDuration, false); break;
                case PlayerState.Caught:
                    if (caughtFrame != null) Play(new[] { caughtFrame }, 1f, false);
                    break;
                default: Play(idleFrames, 2f, true); break;
            }
        }

        static int Frames(Sprite[] f) => f != null && f.Length > 0 ? f.Length : 1;

        void Play(Sprite[] frames, float fps, bool loop)
        {
            if (frames != null && frames.Length > 0) anim.Play(frames, fps, loop);
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
