using System.Collections.Generic;

namespace Sleight
{
    // GDD §13.3：运行时状态。
    public class ActState
    {
        public float time;              // 幕内时间
        public bool armed;              // 呈现已在窗口内完成
        public float lastPresentAt;     // 最近一次完成时刻
        public float suspicion;         // 0..100，幕内累计
        public Dictionary<string, string> propStates = new Dictionary<string, string>();
        public bool judged;
    }

    public struct PlayerSnapshot
    {
        public Zone zone;
        public float assistDistance;
    }

    public enum FailReason
    {
        None,
        PropsMismatch,        // F1
        PlayerOutsideZone,    // F1
        AssistantMissing,     // F1
        PresentMissing,       // F2
        SuspicionFull,        // F3
        Timeout,              // F4
        SetupIncomplete,      // F5（GDD §0.7）：开幕时道具没摆到位
    }

    public struct Verdict
    {
        public bool passed;
        public FailReason reason;

        public static Verdict Pass() => new Verdict { passed = true, reason = FailReason.None };
        public static Verdict Fail(FailReason r) => new Verdict { passed = false, reason = r };
    }

    // GDD §12.1 / §17.3：纯函数判定，按顺序检查，首个失败即返回。不依赖 MonoBehaviour，便于单测。
    public static class Judgement
    {
        public static Verdict Judge(ActData d, ActState s, PlayerSnapshot p, float suspicion)
        {
            if (d.isEnding) return Verdict.Pass();
            if (!s.armed) return Verdict.Fail(FailReason.PresentMissing);
            if (suspicion >= 100f) return Verdict.Fail(FailReason.SuspicionFull);

            foreach (var spec in d.props)
                if (!s.propStates.TryGetValue(spec.slot, out var state) || state != spec.target)
                    return Verdict.Fail(FailReason.PropsMismatch);

            if (p.zone != d.TargetZone) return Verdict.Fail(FailReason.PlayerOutsideZone);

            if (d.requireAssistant && p.assistDistance > 0.5f)
                return Verdict.Fail(FailReason.AssistantMissing);

            return Verdict.Pass();
        }
    }
}
