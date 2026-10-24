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

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
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
            if (sprite != null) sr.sprite = sprite;
        }
    }
}
