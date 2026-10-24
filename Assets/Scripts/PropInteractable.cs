using UnityEngine;

namespace Sleight
{
    // GDD §10：单个道具，二态开关，按一次翻一次，两帧硬切。
    [RequireComponent(typeof(SpriteRenderer))]
    public class PropInteractable : MonoBehaviour
    {
        public string slot;   // 当前所在槽位；"" = 不在槽位上（在道具架上，或被手拿着）
        public string type;   // "box" | "cloth" | "mirror"
        public bool isDecoy;  // GDD §0.5：成品图里没有的干扰道具

        // 0 = 错误态（open / off / normal），1 = 正确态（sealed / on / flipped）。TODO: 填入 §19.2 的道具图。
        public Sprite[] stateSprites = new Sprite[2];
        public Sprite[] ghostSprites = new Sprite[2];   // 成品图虚影（GDD §0.2），同样 0 = 错误态，1 = 正确态

        public string State { get; private set; }

        public float X => transform.position.x;
        public float InteractDuration => type == "cloth" ? 0.5f : 0.6f;
        public Bounds Bounds => sr.bounds;
        public bool Held { get; private set; }

        public const float SnapDistance = 0.3f;   // GDD §0.3

        SpriteRenderer sr;
        Color baseColor;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
        }

        public void Init(PropSpec spec, bool decoy = false)
        {
            type = spec.type;
            isDecoy = decoy;
            ResetTo(spec);
        }

        // 回到本幕数据里的起点：有 startX 就放在道具架上，否则直接摆在 slot 上。
        public void ResetTo(PropSpec spec)
        {
            Held = false;
            SetState(spec.initial);
            if (spec.startX > 0f) Place(spec.startX, "");
            else Place(Stage.SlotX(spec.slot), spec.slot);
        }

        public void SetState(string s)
        {
            State = s;
            Apply();
        }

        public void Place(float x, string slotName)
        {
            slot = slotName;
            transform.position = new Vector3(x, Stage.Floor, 0f);
        }

        public void Lift()
        {
            Held = true;
            slot = "";
        }

        // 被手拿着：道具顶端挂在手下面一点，底边不低于地面线。
        public void FollowHand(Vector3 hand)
        {
            float height = sr.bounds.size.y;
            float y = Mathf.Max(Stage.Floor, hand.y - height + 0.1f);
            transform.position = new Vector3(hand.x, y, -0.05f);
        }

        // 松手：竖直落到地面线；离空着的槽位不超过 SnapDistance 就吸附过去。返回是否吸附。
        public bool Drop(System.Func<string, bool> slotFree)
        {
            Held = false;
            float x = transform.position.x;
            string best = "";
            float bestDist = SnapDistance;
            foreach (var s in Stage.PropSlots)
            {
                float d = Mathf.Abs(Stage.SlotX(s) - x);
                if (d <= bestDist && slotFree(s))
                {
                    best = s;
                    bestDist = d;
                }
            }
            Place(best != "" ? Stage.SlotX(best) : x, best);
            return best != "";
        }

        public void Toggle()
        {
            State = Opposite(State);
            Apply();
        }

        static string Opposite(string s)
        {
            switch (s)
            {
                case "open": return "sealed";
                case "sealed": return "open";
                case "off": return "on";
                case "on": return "off";
                case "normal": return "flipped";
                case "flipped": return "normal";
                default: return s;
            }
        }

        public static bool IsSecondState(string s) => s == "sealed" || s == "on" || s == "flipped";

        void Apply()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            var sprite = stateSprites != null && stateSprites.Length == 2 ? stateSprites[IsSecondState(State) ? 1 : 0] : null;
            if (sprite != null)
            {
                sr.sprite = sprite;
                return;
            }
            // 占位：没有正式图时按 §19.2 的尺寸画色块（像素 / 64 = 单位）。
            transform.localScale = PlaceholderSize(State) / 64f;
            sr.color = State == "flipped" ? MirrorEdge : baseColor;
        }

        static readonly Color MirrorEdge = new Color32(0x7C, 0x7C, 0x84, 0xFF);   // 镜面朝外的亮边

        static Vector3 PlaceholderSize(string s)
        {
            switch (s)
            {
                case "open": return new Vector3(48, 56, 64);
                case "sealed": return new Vector3(48, 44, 64);
                case "off": return new Vector3(72, 40, 64);
                case "on": return new Vector3(72, 56, 64);
                default: return new Vector3(40, 112, 64);   // mirror
            }
        }
    }
}
