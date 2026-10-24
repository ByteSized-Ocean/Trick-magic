using UnityEditor;
using UnityEngine;

namespace Sleight.EditorTools
{
    // GDD §18.6：Assets/Art 下的每一张图自动套用同一套导入设置，不用手动改。
    public class SpriteImportRules : AssetPostprocessor
    {
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
            settings.spriteAlignment = (int)PivotFor(System.IO.Path.GetFileName(assetPath));
            ti.SetTextureSettings(settings);
        }

        // 脚部/底边：Bottom Center；追光：Top Center；其余居中。
        static SpriteAlignment PivotFor(string file)
        {
            if (file.StartsWith("spot_")) return SpriteAlignment.TopCenter;
            if (file.StartsWith("player_") || file.StartsWith("prop_")) return SpriteAlignment.BottomCenter;
            return SpriteAlignment.Center;
        }
    }
}
