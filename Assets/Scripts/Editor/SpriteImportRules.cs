using UnityEditor;
using UnityEngine;

namespace Sleight.EditorTools
{
    // GDD §18.6：Assets/Art 下的每一张图自动套用同一套导入设置，不用手动改。
    public class SpriteImportRules : AssetPostprocessor
    {
        // 改了规则就加 1，Unity 会按新规则重新导入已有的图。
        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 64;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            string file = System.IO.Path.GetFileName(assetPath);
            if (file.StartsWith("audience_head_"))
            {
                // 观众的头绕脖子转：pivot 在 gen_art.py 的 NECK_Y（自上而下第 60/96 行）。
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 1f - 60f / 96f);
            }
            else
            {
                settings.spriteAlignment = (int)PivotFor(file);
            }
            ti.SetTextureSettings(settings);
        }

        // 脚部/底边：Bottom Center；追光：Top Center；其余居中。
        static SpriteAlignment PivotFor(string file)
        {
            if (file.StartsWith("spot_")) return SpriteAlignment.TopCenter;
            if (file.StartsWith("player_") || file.StartsWith("audience_") || (file.StartsWith("prop_") && !file.StartsWith("prop_marker"))) return SpriteAlignment.BottomCenter;
            return SpriteAlignment.Center;
        }
    }
}
