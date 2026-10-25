using UnityEngine;
using UnityEngine.Rendering;

namespace Sleight.RabbitTrick
{
    public static class IllusionScenery
    {
        public static void Build(RabbitStageView view, Transform parent, int theme)
        {
            var scene = view.Group("Scenery", parent, Vector2.zero);
            scene.gameObject.AddComponent<SortingGroup>().sortingOrder = -10;
            Color[] walls = { new Color32(28, 40, 56, 255), new Color32(19, 48, 57, 255), new Color32(56, 29, 63, 255), new Color32(17, 27, 49, 255), new Color32(33, 36, 52, 255) };
            Color gold = new Color32(198, 156, 88, 255), cool = new Color32(83, 130, 148, 255);
            view.Rect("NewBackdrop", scene, new Vector2(5, 3.5f), new Vector2(10, 5.1f), walls[theme], 0);
            view.Rect("NewFloor", scene, new Vector2(5, 1.28f), new Vector2(10, 1.12f), theme == 3 ? new Color32(47, 59, 76, 255) : new Color32(100, 79, 69, 255), 1);
            for (int i = 0; i < 17; i++)
                view.Rect("FloorJoint", scene, new Vector2(i * 0.63f, 1.26f), new Vector2(0.014f, 1.08f), new Color(0.1f, 0.12f, 0.17f, 0.6f), 2);
            if (theme == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = 1.4f + i * 2.4f;
                    view.Rect("GalleryFrame", scene, new Vector2(x, 3.8f), new Vector2(1.5f, 1.5f), gold, 3);
                    view.Rect("Canvas", scene, new Vector2(x, 3.8f), new Vector2(1.37f, 1.37f), new Color32(59, 71, 90, 255), 4);
                    var art = view.Rect("CubistPainting", scene, new Vector2(x, 3.8f), new Vector2(0.6f, 0.85f), i % 2 == 0 ? cool : new Color32(149, 74, 83, 255), 5);
                    art.transform.localRotation = Quaternion.Euler(0, 0, 25 + i * 20);
                }
            }
            else if (theme == 1)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = 0.8f + i * 2.15f;
                    view.Rect("IndustrialPipe", scene, new Vector2(x, 3.7f), new Vector2(0.1f, 3.4f), cool, 3);
                    for (int j = 0; j < 3; j++) view.Rect("PipeCollar", scene, new Vector2(x, 2.4f + j * 0.9f), new Vector2(0.2f, 0.07f), gold, 4);
                }
                view.Rect("OverheadPipe", scene, new Vector2(5, 4.9f), new Vector2(8.8f, 0.11f), cool, 4);
            }
            else if (theme == 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = 0.7f + i * 1.43f;
                    view.Rect("MirrorBorder", scene, new Vector2(x, 3.5f), new Vector2(0.95f, 2.8f), gold, 3);
                    view.Rect("MirrorGlass", scene, new Vector2(x, 3.5f), new Vector2(0.83f, 2.66f), new Color32(58, 69, 94, 255), 4);
                    var shine = view.Rect("Reflection", scene, new Vector2(x - 0.15f, 3.6f), new Vector2(0.06f, 2.3f), new Color(0.7f, 0.8f, 0.95f, 0.12f), 5);
                    shine.transform.localRotation = Quaternion.Euler(0, 0, -12);
                }
            }
            else if (theme == 3)
            {
                for (int i = 0; i < 15; i++)
                {
                    float height = 1.2f + Mathf.Sin(i * 2.1f) * 0.7f;
                    float x = 0.2f + i * 0.7f;
                    view.Rect("Skyline", scene, new Vector2(x, 1.8f + height * 0.5f), new Vector2(0.6f, height), new Color32(27, 39, 59, 255), 3);
                    for (int j = 0; j < 3; j++) view.Rect("Window", scene, new Vector2(x, 2f + j * 0.3f), new Vector2(0.09f, 0.1f), gold * new Color(1, 1, 1, 0.65f), 4);
                }
                view.Disc("Moon", scene, new Vector2(8.1f, 4.5f), new Vector2(0.65f, 0.65f), new Color32(154, 166, 186, 255), 3);
                view.Rect("RooftopRail", scene, new Vector2(5, 1.95f), new Vector2(10, 0.06f), cool, 5);
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    float x = 0.6f + i * 1.8f;
                    view.Rect("MarblePillar", scene, new Vector2(x, 3.1f), new Vector2(0.35f, 3.2f), new Color32(133, 141, 154, 255), 3);
                    view.Rect("PillarCapital", scene, new Vector2(x, 4.7f), new Vector2(0.58f, 0.14f), gold, 4);
                    view.Rect("PillarFoot", scene, new Vector2(x, 1.54f), new Vector2(0.58f, 0.14f), gold, 4);
                }
                view.Rect("ArtDecoLintel", scene, new Vector2(5, 4.98f), new Vector2(9.8f, 0.25f), gold, 4);
            }
        }
    }
}
