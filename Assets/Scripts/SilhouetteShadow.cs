using UnityEngine;
using UnityEngine.Rendering;

namespace Sleight
{
    // 纸片剪影本身不受光（保持纯黑可读），另挂一张只投影子的面片，让追光把影子打到天幕和地板上。
    // 面片用剪影当前那一帧的贴图做 alpha 裁剪，所以影子的形状跟着动画走。
    public class SilhouetteShadow : MonoBehaviour
    {
        public Material shadowMaterial;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        SpriteRenderer sr;
        Transform card;
        MeshRenderer cardRenderer;
        MaterialPropertyBlock block;
        Sprite shownSprite;
        bool shownFlip;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (shadowMaterial == null || sr == null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ShadowCard";
            Destroy(go.GetComponent<Collider>());
            card = go.transform;
            card.SetParent(transform, false);
            cardRenderer = go.GetComponent<MeshRenderer>();
            cardRenderer.sharedMaterial = shadowMaterial;
            cardRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            cardRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
            Sync();
        }

        void LateUpdate()
        {
            if (card != null && (sr.sprite != shownSprite || sr.flipX != shownFlip)) Sync();
        }

        void Sync()
        {
            shownSprite = sr.sprite;
            shownFlip = sr.flipX;
            if (shownSprite == null) return;

            var b = shownSprite.bounds;
            card.localPosition = new Vector3(shownFlip ? -b.center.x : b.center.x, b.center.y, 0f);
            card.localScale = new Vector3(shownFlip ? -b.size.x : b.size.x, b.size.y, 1f);
            block.SetTexture(BaseMap, shownSprite.texture);
            cardRenderer.SetPropertyBlock(block);
        }
    }
}
