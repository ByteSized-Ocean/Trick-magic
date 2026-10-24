using UnityEngine;

namespace Sleight.RabbitTrick
{
    // 第 2、3 关的魔术。第 1 关（空箱变兔子）沿用 RabbitTrickGame + RabbitTrickRules 原来的写法；
    // 后面的关卡各自实现这一套：自己的道具、判定、提示和画面，和第 1 关共用双手、走位、追光、耐心和落幕。
    // 道具的画面在运行时用 RabbitStageView 的方块 / 圆片拼出来，画风和第 1 关一致。
    public abstract class StageTrick
    {
        public abstract string Name { get; }            // 显示在右上角，如 "THE SUBSTITUTION CABINET"
        public abstract string SuccessTitle { get; }    // 成功面板的大标题
        public abstract string[] StepNames { get; }     // 三步，带编号
        public virtual float StartLightX => 5f;

        public int Step { get; protected set; }
        public bool Finished => Step >= StepNames.Length;
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; } = "";
        public float StepProgress { get; protected set; }   // 当前这一步的进度条（0..1），不需要时为 0

        protected RabbitTrickGame game;
        protected RabbitStageView view;
        protected Transform root;

        public void Attach(RabbitTrickGame g)
        {
            game = g;
            view = g.view;
            if (root == null)
            {
                var previous = view.World.Find(GetType().Name);
                if (previous != null)
                {
                    previous.gameObject.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(previous.gameObject);
                    else Object.DestroyImmediate(previous.gameObject);
                }
                root = view.Group(GetType().Name, view.World, Vector2.zero);
                Build();
                foreach (Transform item in root) StageDepth.Set(item, 0.2f);
            }
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
        }

        public void Reset()
        {
            Step = 0;
            Failed = false;
            FailureReason = "";
            StepProgress = 0f;
            OnReset();
        }

        public void Expose(string reason)
        {
            if (Failed || Finished) return;
            Failed = true;
            FailureReason = reason;
        }

        protected abstract void Build();
        protected abstract void OnReset();
        public abstract void Tick(float dt);
        public abstract void Draw();
        public abstract string Hint();
        public abstract void AddDemo(TrickDemo demo);   // 开场演示：按正确解法演一遍
        public virtual void DirectAudience(float dt) => game.Audience.Follow(game.Hands.BodyPosition.x, 8f, "MAGICIAN");
        protected virtual LooseProp[] MovableProps => System.Array.Empty<LooseProp>();
        public virtual float AnchorDepth(TrickHand hand) => 0f;
        public void SyncHeldProps()
        {
            foreach (var p in MovableProps)
            {
                if (!p.Held) continue;
                p.depth = game.Hands.Depth;
                p.floorY = p.frontFloor + p.depth * DualHandController.LaneLift;
                p.Follow(game.Hands.Hands[p.heldBy].position);
            }
        }

        // 手：按住 E 时调用，抓到东西就把 hand.holding 设成 Custom、hand.custom 设成道具编号。
        public abstract bool TryGrab(TrickHand hand, int index);
        public abstract void MoveHeld(TrickHand hand, int index, Vector2 delta);
        public abstract void Release(TrickHand hand, int index);
        // 抓着的是固定在场景里的把手（门、拉杆）时返回 true 和把手位置：身体走开时不能把它拖走。
        public abstract bool Anchor(TrickHand hand, out Vector2 position);

        protected bool Lit(Vector2 point, float radius = 0f) => game.Audience.Illuminates(point, radius);
        protected bool InBack => game.Hands.InBack;

        // 拿着的道具跟着手所在的那一层；松手时落到那一层的地面
        protected void FollowLane(LooseProp p, float frontFloor)
        {
            p.frontFloor = frontFloor;
            if (p.Held) p.depth = game.Hands.Depth;
            p.floorY = frontFloor + p.depth * DualHandController.LaneLift;
        }

        // 在后侧的道具整体往后画（排序往下压），会被前侧的柜子、桌子挡住
        protected void SetBehind(Transform t, bool behind)
            => SetDepth(t, behind ? 1f : 0f);
        protected void SetDepth(Transform t, float depth)
        {
            StageDepth.Set(t, depth, 3);
        }
        protected static bool InWings(float x) => x < 0.95f || x > 9.05f;   // 两侧幕布后面，观众看不见
    }

    // 能拿起来到处搬的道具：被手拿着时跟着手走，松手后受重力落到地上、弹一下。
    public sealed class LooseProp
    {
        public Vector2 position;     // 道具底边中点
        public Vector2 velocity;
        public Rect shape;           // 相对 position 的外形，用来判断手有没有碰到、灯有没有照到
        public float floorY;
        public int heldBy = -1;
        public bool inside;          // 在柜子里
        public float depth;          // Continuous stage depth: 0 at the front, 1 at the back.
        public float frontFloor = DualHandController.FrontY;
        public bool back { get => depth > 0.5f; set => depth = value ? 1f : 0f; }
        public Vector2 grip;         // 被抓时道具相对手的位置

        public bool Held => heldBy >= 0;
        public Vector2 Center => position + shape.center;

        public float DistanceTo(Vector2 p)
        {
            Vector2 local = p - position;
            float dx = Mathf.Max(shape.xMin - local.x, 0f, local.x - shape.xMax);
            float dy = Mathf.Max(shape.yMin - local.y, 0f, local.y - shape.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public bool Visible(RandomAudienceLight light) =>
            !back && light.Illuminates(Center, Mathf.Min(shape.width, shape.height) * 0.4f);

        public void Grab(int hand, Vector2 handPosition)
        {
            heldBy = hand;
            inside = false;
            velocity = Vector2.zero;
            grip = position - handPosition;
        }

        public void Follow(Vector2 handPosition) => position = handPosition + grip;

        public void Drop()
        {
            heldBy = -1;
            velocity = Vector2.zero;
        }

        public void Tick(float dt)
        {
            if (Held || inside) return;
            if (position.y <= floorY && velocity.y <= 0f) { position.y = floorY; return; }
            velocity.y -= 9.8f * dt;
            position += velocity * dt;
            if (position.y <= floorY)
            {
                position.y = floorY;
                velocity = velocity.y < -1.2f ? new Vector2(0f, -velocity.y * 0.28f) : Vector2.zero;
            }
        }
    }
}
