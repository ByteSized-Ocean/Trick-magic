using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sleight
{
    // GDD §0.2：成品图。开幕前用本幕道具的"目标状态"剪影，在各自槽位上画一层旧纸色的虚影，
    // 停一下再淡掉。不另外出图，也没有文字。
    public class PreviewCard : MonoBehaviour
    {
        public Color ghostColor = new Color(1f, 1f, 1f, 0.7f);
        public float hold = 2.0f;
        public float fade = 0.5f;

        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();

        public IEnumerator Play(ActData d, System.Func<string, PropInteractable> prefabFor)
        {
            Clear();
            foreach (var spec in d.props)
            {
                var prefab = prefabFor(spec.type);
                if (prefab == null) continue;
                var src = prefab.GetComponent<SpriteRenderer>();
                var go = new GameObject("Ghost_" + spec.slot);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(Stage.SlotX(spec.slot), Stage.Floor, 0.05f);
                var sr = go.AddComponent<SpriteRenderer>();
                int i = PropInteractable.IsSecondState(spec.target) ? 1 : 0;
                var sprites = prefab.ghostSprites;
                sr.sprite = sprites != null && sprites.Length == 2 && sprites[i] != null ? sprites[i] : src.sprite;
                if (sr.sprite == src.sprite) go.transform.localScale = prefab.transform.localScale;   // 没有虚影图时退回色块
                sr.sharedMaterial = src.sharedMaterial;
                sr.sortingLayerName = "Props";
                sr.sortingOrder = 5;
                sr.color = ghostColor;
                ghosts.Add(sr);
            }

            yield return new WaitForSeconds(hold);
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                foreach (var g in ghosts)
                {
                    var c = ghostColor;
                    c.a *= 1f - t / fade;
                    g.color = c;
                }
                yield return null;
            }
            Clear();
        }

        public void Clear()
        {
            foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
            ghosts.Clear();
        }
    }
}
