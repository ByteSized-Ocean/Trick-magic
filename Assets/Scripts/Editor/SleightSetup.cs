using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sleight.EditorTools
{
    // GDD §18：一键搭建工程设置和单场景 Scenes/Game.unity。可重复执行，重跑会覆盖 Game.unity。
    public static class SleightSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string PlaceholderPath = "Assets/Art/Placeholder/Placeholder.asset";
        const string PropPrefabPath = "Assets/Prefabs/P_Prop.prefab";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static readonly string[] SortingLayers = { "BG", "Stage", "Props", "Player", "Light", "HUD" };

        // GDD §5.1 调色板
        static readonly Color Silhouette = Hex(0x0B0B0C);
        static readonly Color Wall = Hex(0x2E2E33);
        static readonly Color Floor = Hex(0x55555C);
        static readonly Color FootLine = Hex(0x7C7C84);
        static readonly Color Divider = Hex(0x3A3A40);
        static readonly Color SpotYellow = Hex(0xFFD21E, 0.55f);
        static readonly Color ClearColor = Hex(0x101012);

        static Sprite centerSprite;
        static Sprite bottomSprite;
        static Material unlit;

        [MenuItem("Sleight/搭建 Game 场景")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Sleight", $"{ScenePath} 已存在，要覆盖吗？", "覆盖", "取消"))
                return;
            Build();
            EditorUtility.DisplayDialog("Sleight", "Game 场景已生成，工程设置已更新。", "好");
        }

        // 批处理入口：Unity -batchmode -executeMethod Sleight.EditorTools.SleightSetup.Build
        public static void Build()
        {
            ApplyProjectSettings();
            EnsureSortingLayers();
            EnsurePlaceholderSprites();
            unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            var propPrefab = EnsurePropPrefab();
            BuildScene(propPrefab);
            AssetDatabase.SaveAssets();
        }

        // §18.1 第 4、5 条：640×360 窗口、关抗锯齿。
        static void ApplyProjectSettings()
        {
            PlayerSettings.defaultScreenWidth = 640;
            PlayerSettings.defaultScreenHeight = 360;
            PlayerSettings.defaultWebScreenWidth = 640;
            PlayerSettings.defaultWebScreenHeight = 360;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp)
                {
                    urp.msaaSampleCount = 1;
                    EditorUtility.SetDirty(urp);
                }
            }
            QualitySettings.SetQualityLevel(current, false);

            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main)
            {
                main.msaaSampleCount = 1;
                EditorUtility.SetDirty(main);
            }
        }

        // §5.2 排序层
        static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var name in SortingLayers)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                        exists = true;
                if (exists) continue;

                layers.InsertArrayElementAtIndex(layers.arraySize);
                var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                layer.FindPropertyRelative("name").stringValue = name;
                layer.FindPropertyRelative("uniqueID").intValue = (int)(name.GetHashCode() & 0x7FFFFFFF);
                layer.FindPropertyRelative("locked").intValue = 0;
            }
            tagManager.ApplyModifiedProperties();
        }

        // 色块占位图，正式美术到位前用。存成 .asset，不走 LFS。
        static void EnsurePlaceholderSprites()
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(PlaceholderPath);
            foreach (var o in existing)
            {
                if (o is Sprite s && s.name == "white_center") centerSprite = s;
                if (o is Sprite b && b.name == "white_bottom") bottomSprite = b;
            }
            if (centerSprite != null && bottomSprite != null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderPath));
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Placeholder", filterMode = FilterMode.Point };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, PlaceholderPath);

            centerSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            centerSprite.name = "white_center";
            bottomSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4);
            bottomSprite.name = "white_bottom";
            AssetDatabase.AddObjectToAsset(centerSprite, tex);
            AssetDatabase.AddObjectToAsset(bottomSprite, tex);
            AssetDatabase.SaveAssets();
        }

        static PropInteractable EnsurePropPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PropPrefabPath));
            var go = new GameObject("P_Prop");
            AddSprite(go, bottomSprite, Silhouette, "Props", 0);
            go.transform.localScale = new Vector3(0.75f, 0.8f, 1f);   // TODO: 按 §19.2 换成正式道具图
            go.AddComponent<PropInteractable>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PropPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<PropInteractable>();
        }

        // §18.2 层级结构
        static void BuildScene(PropInteractable propPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // MainCamera：正交、固定，不挂任何相机脚本（§4.1）。
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(5f, 2.8f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.8125f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ClearColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.antialiasing = AntialiasingMode.None;

            var stage = new GameObject("Stage").transform;
            Block("BG_Wall", stage, new Vector2(5f, 2.8f), new Vector2(10f, 5.625f), Wall, "BG", 0);
            Block("BG_Floor", stage, new Vector2(5f, 0.51f), new Vector2(10f, 0.98f), Floor, "BG", 1);
            Block("BG_FootLine", stage, new Vector2(5f, 1.0f), new Vector2(10f, 0.02f), FootLine, "BG", 2);
            Block("BG_Divider", stage, new Vector2(5f, 2.6f), new Vector2(0.02f, 3.2f), Divider, "BG", 3);

            var lights = new GameObject("Spotlights").transform;
            var spotGo = Block("Light_Main", lights, new Vector2(2.5f, 2.6f), new Vector2(4.5f, 3.2f), SpotYellow, "Light", 0);
            var spotlight = spotGo.AddComponent<Spotlight>();

            var props = new GameObject("Props").transform;

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0.8f, 1.0f, 0f);
            playerGo.transform.localScale = new Vector3(1.0f, 3.0f, 1f);   // TODO: 换成 128×192 的玩家帧
            AddSprite(playerGo, bottomSprite, Silhouette, "Player", 0);
            playerGo.AddComponent<FrameAnim>();
            var player = playerGo.AddComponent<PlayerController>();
            player.spotlight = spotlight;

            var hud = BuildHud();

            var game = new GameObject("Game");
            var flow = game.AddComponent<GameFlow>();
            var runner = game.AddComponent<ActRunner>();
            flow.runner = runner;
            flow.hud = hud;
            runner.flow = flow;
            runner.spotlight = spotlight;
            runner.player = player;
            runner.hud = hud;
            runner.propsRoot = props;
            runner.propPrefab = propPrefab;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static HudAudio BuildHud()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<HudAudio>();
            hud.sfxSource = hudGo.AddComponent<AudioSource>();
            hud.sfxSource.playOnAwake = false;
            hud.heartbeatSource = hudGo.AddComponent<AudioSource>();
            hud.heartbeatSource.playOnAwake = false;
            hud.heartbeatSource.loop = true;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(hudGo.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // §4.4：左下角起点 (20, 330)，直径 10px，间距 12px。
            var dots = new GameObject("ActDots", typeof(RectTransform)).GetComponent<RectTransform>();
            dots.SetParent(canvasGo.transform, false);
            for (int i = 0; i < 3; i++)
            {
                var dot = UiImage($"Dot{i + 1}", dots, Hex(0xD0D0D6, 0.25f));
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(10, 10);
                rt.anchoredPosition = new Vector2(20 + i * 22, -330 + 10);
                hud.actDots[i] = dot;
            }

            hud.flashWhite = FullScreen("FlashWhite", canvasGo.transform, new Color(1, 1, 1, 0));
            hud.fadeBlack = FullScreen("FadeBlack", canvasGo.transform, new Color(0, 0, 0, 0));
            return hud;
        }

        static Image FullScreen(string name, Transform parent, Color color)
        {
            var img = UiImage(name, parent, color);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        static Image UiImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static GameObject Block(string name, Transform parent, Vector2 center, Vector2 size, Color color, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            AddSprite(go, centerSprite, color, layer, order);
            return go;
        }

        static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, string layer, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            if (unlit != null) sr.sharedMaterial = unlit;
            return sr;
        }

        static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }
}
