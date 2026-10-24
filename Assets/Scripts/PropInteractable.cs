using UnityEngine;

namespace Sleight
{
    // GDD §10：单个道具，二态开关，按一次翻一次，两帧硬切。
    [RequireComponent(typeof(SpriteRenderer))]
    public class PropInteractable : MonoBehaviour
    {
        public string slot;
        public string type;   // "box" | "cloth" | "mirror"

        // 0 = 错误态（open / off / normal），1 = 正确态（sealed / on / flipped）。TODO: 填入 §19.2 的道具图。
        public Sprite[] stateSprites = new Sprite[2];

        public string State { get; private set; }

        public float X => transform.position.x;
        public float InteractDuration => type == "cloth" ? 0.5f : 0.6f;

        SpriteRenderer sr;
        Color baseColor;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
        }

        public void Init(PropSpec spec)
        {
            slot = spec.slot;
            type = spec.type;
            State = spec.initial;
            var pos = transform.position;
            transform.position = new Vector3(Stage.SlotX(slot), Stage.Floor, pos.z);
            Apply();
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

        static bool IsSecondState(string s) => s == "sealed" || s == "on" || s == "flipped";

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
