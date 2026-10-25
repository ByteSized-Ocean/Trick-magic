using System;
using UnityEngine;

namespace Sleight.RabbitTrick
{
    // Scores are attached to programme positions; records follow the trick if its position changes.
    public sealed class ShowScore
    {
        public const int PointsPerSecond = 100;
        readonly int[] points = new int[RabbitTrickGame.LevelCount];
        readonly bool[] completed = new bool[RabbitTrickGame.LevelCount];
        readonly int[] best = new int[RabbitTrickGame.LevelCount];
        readonly string prefix;
        public bool SaveRecords { get; set; } = true;
        public int BestFullShow { get; private set; }
        public int Total { get { int sum = 0; foreach (int value in points) sum += value; return sum; } }
        public int CompletedCount { get { int count = 0; foreach (bool value in completed) if (value) count++; return count; } }

        public ShowScore(string preferencePrefix = "Sleight.Score.") { prefix = preferencePrefix; }

        public void LoadRecords()
        {
            for (int i = 0; i < best.Length; i++) best[i] = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "Trick." + i, 0));
            BestFullShow = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "FullShow", 0));
        }

        public static int FromTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
            // Floor to a hundredth: the displayed remaining time and the bonus use the same integer.
            return Mathf.FloorToInt(Mathf.Clamp(seconds, 0f, 3600f) * PointsPerSecond + 0.0001f);
        }

        public int ForLevel(int level) => points[Mathf.Clamp(level, 0, points.Length - 1)];
        public bool Completed(int level) => completed[Mathf.Clamp(level, 0, completed.Length - 1)];
        public int BestForTrick(int trickId) => best[Mathf.Clamp(trickId, 0, best.Length - 1)];
        public void BeginShow() { Array.Clear(points, 0, points.Length); Array.Clear(completed, 0, completed.Length); }
        public void ClearLevel(int level) { points[level] = 0; completed[level] = false; }

        public void RecordWin(int level, int trickId, float remainingSeconds)
        {
            // One award per attempt, even if the success state lasts for many frames.
            if (completed[level]) return;
            points[level] = FromTime(remainingSeconds); completed[level] = true;
            if (points[level] > best[trickId])
            {
                best[trickId] = points[level];
                if (SaveRecords) { PlayerPrefs.SetInt(prefix + "Trick." + trickId, best[trickId]); PlayerPrefs.Save(); }
            }
        }

        public void FinishShow()
        {
            // Starting from a later level is allowed, but is not a full-show record.
            if (CompletedCount != RabbitTrickGame.LevelCount || Total <= BestFullShow) return;
            BestFullShow = Total;
            if (SaveRecords) { PlayerPrefs.SetInt(prefix + "FullShow", BestFullShow); PlayerPrefs.Save(); }
        }
    }
}
