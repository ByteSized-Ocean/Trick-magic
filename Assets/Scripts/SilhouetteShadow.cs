using UnityEngine;
using UnityEngine.Rendering;

namespace Sleight
{
    // 纸片剪影本身不受光（保持纯黑可读），另挂一张只投影子的面片，让追光把影子打到天幕和地板上。
    // TODO: 换正式图后改成按贴图 alpha 裁剪的投影材质，影子形状才会跟剪影一致。
    public class SilhouetteShadow : MonoBehaviour
    {
        public Material shadowMaterial;

        void Awake()
        {
            if (shadowMaterial == null) return;
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "ShadowCard";
            Destroy(card.GetComponent<Collider>());
            card.transform.SetParent(transform, false);
            card.transform.localPosition = new Vector3(0f, 0.5f, 0f);   // 占位图脚底 pivot，面片中心在半高
            var mr = card.GetComponent<MeshRenderer>();
            mr.sharedMaterial = shadowMaterial;
            mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            mr.receiveShadows = false;
        }
    }
}
