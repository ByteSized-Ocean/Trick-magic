using System;
using UnityEngine;

namespace Sleight
{
    // GDD §13.1：幕数据，从 Resources/acts.json 读取（§18.5）。
    [Serializable]
    public class ActData
    {
        public string actId;          // "L1A1"
        public int level;             // 1..3
        public int index;             // 1..3（结局幕为 0）
        public float playerStart;     // x
        public string targetZone;     // "A" | "B" | ""（结局幕）
        public PropSpec[] props;      // 本幕实例化的道具
        public bool requireAssistant;
        public LightSpec[] lights;    // v1 长度 1
        public float actTimeLimit = 45f;
        public bool isEnding;
        public string designNote;     // 给美术/关卡看的备注，游戏内不显示

        public Zone TargetZone => targetZone == "A" ? Zone.A : Zone.B;

        public static ActData[] LoadAll()
        {
            var asset = Resources.Load<TextAsset>("acts");
            if (asset == null)
            {
                Debug.LogError("[ActData] Resources/acts.json 不存在");
                return Array.Empty<ActData>();
            }
            return JsonUtility.FromJson<ActWrapper>(asset.text).acts;
        }

        [Serializable]
        class ActWrapper
        {
            public ActData[] acts;
        }
    }

    [Serializable]
    public class PropSpec
    {
        public string slot;     // "B.box" / "A.cloth" / "B.mirror"
        public string type;     // "box" | "cloth" | "mirror"
        public string initial;  // "open" | "sealed" | "on" | "off" | "normal" | "flipped"
        public string target;   // 同上
    }

    [Serializable]
    public class LightSpec
    {
        public string id;
        public string startZone;   // "A" | "B"
        public float dwell = 3.0f;
        public float transition = 0.4f;
        public float startDelay = 0f;
    }

    public enum Zone { A, B }

    // GDD §4.2 / §4.3：世界坐标与槽位，唯一权威。
    public static class Stage
    {
        public const float Left = 0f;
        public const float Right = 10f;
        public const float Divider = 5.0f;
        public const float Floor = 1.0f;
        public const float Ceiling = 4.2f;

        public static Zone ZoneOf(float x) => x < Divider ? Zone.A : Zone.B;
        public static float ZoneCenter(Zone z) => z == Zone.A ? 2.5f : 7.5f;
        public static Zone Other(Zone z) => z == Zone.A ? Zone.B : Zone.A;

        public static float SlotX(string slot)
        {
            switch (slot)
            {
                case "A.box": return 1.4f;
                case "A.cloth": return 2.6f;
                case "A.assist": return 3.6f;
                case "A.mirror": return 4.4f;
                case "B.box": return 6.2f;
                case "B.cloth": return 7.4f;
                case "B.assist": return 8.4f;
                case "B.mirror": return 9.2f;
                default: throw new ArgumentException($"未知槽位 {slot}");
            }
        }

        public static float AssistX(Zone z) => SlotX(z == Zone.A ? "A.assist" : "B.assist");
    }
}
