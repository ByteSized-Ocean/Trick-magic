using UnityEngine;
using UnityEngine.Rendering;

namespace Sleight.RabbitTrick
{
    // A single depth rule for whole silhouettes; fingers, sleeves and prop details stay together.
    public static class StageDepth
    {
        public static int Order(float depth) => 10000 - Mathf.RoundToInt(Mathf.Clamp01(depth) * 8000f);
        public static void Set(Transform item, float depth, int bias = 0)
        {
            if (item == null) return;
            var group = item.GetComponent<SortingGroup>();
            if (group == null) group = item.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = Order(depth) + bias;
            var p = item.position; p.z = depth * 1.5f; item.position = p;
        }
        public static void Foreground(Transform item, int detailOrder = 0)
        {
            var group = item.GetComponent<SortingGroup>();
            if (group == null) group = item.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = 20000 + detailOrder;
        }
    }
}
