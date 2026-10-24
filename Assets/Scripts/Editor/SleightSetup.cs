using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sleight.EditorTools
{
    // GDD §18：一键搭建工程设置和单场景 Scenes/Game.unity。可重复执行，重跑会覆盖 Game.unity。
    public static class SleightSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string PlaceholderPath = "Assets/Art/Placeholder/Placeholder.asset";
        const string CirclePath = "Assets/Art/Placeholder/Circle.asset";
        const string PropPrefabPath = "Assets/Prefabs/P_Prop.prefab";
        const string PipelinePath = "Assets/Settings/UniversalRP_3D.asset";
        const string RendererPath = "Assets/Settings/UniversalRenderer_3D.asset";
        const string MaterialDir = "Assets/Art/Materials";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static readonly string[] SortingLayers = { "BG", "Stage", "Props", "Player", "Light", "HUD", "Audience" };

        // GDD §5.1 调色板
        static readonly Color Silhouette = Hex(0x0B0B0C);
        static readonly Color Wall = Hex(0x2E2E33);
        static readonly Color Floor = Hex(0x55555C);
        static readonly Color FootLine = Hex(0x7C7C84);
        static readonly Color Divider = Hex(0x3A3A40);
        static readonly Color SpotYellow = Hex(0xFFD21E);
        static readonly Color Ambient = Hex(0x5C5C66);   // 暗区要暗，追光才显眼
        static readonly Color ClearColor = Hex(0x101012);

        static Sprite centerSprite;
        static Sprite bottomSprite;
        static Sprite circleSprite;
        static Material unlit;
        static Material shadowCaster;

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
            EnsureUniversalRenderer();
            ApplyProjectSettings();
            EnsureSortingLayers();
            EnsurePlaceholderSprites();
            EnsureCircleSprite();
            unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            shadowCaster = LitMaterial("M_ShadowCaster", Silhouette);
            var propPrefab = EnsurePropPrefab();
            BuildScene(propPrefab);
            AssetDatabase.SaveAssets();
        }

        // 3D 舞台要真光照和阴影：把 2D Renderer 换成 Universal Renderer，打开附加光（追光）的阴影。
        static void EnsureUniversalRenderer()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }

            var so = new SerializedObject(asset);
            SetInt(so, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
            SetBool(so, "m_SoftShadowsSupported", false);   // 追光硬边（§5.3）
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = v;
            else Debug.LogWarning($"[SleightSetup] URP 资源上没有 {name}，请手动检查");
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v;
            else Debug.LogWarning($"[SleightSetup] URP 资源上没有 {name}，请手动检查");
        }

        static Material ConeMaterial()
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/M_LightCone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            // 透明、双面、不写深度的 Unlit。
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(1f, 0.824f, 0.118f, 0.35f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
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
            go.AddComponent<SilhouetteShadow>().shadowMaterial = shadowCaster;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PropPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<PropInteractable>();
        }

        // §18.2 层级结构
        static void BuildScene(PropInteractable propPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // MainCamera：正交、固定，不挂任何相机脚本（§4.1）。
            // 正交不变，略微俯视 8°，让地板和舞台进深露出来。只绕 x 轴转，左右坐标和 2D 时完全一致。
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            camGo.transform.position = new Vector3(5f, 2.8f, 0.5f) - camGo.transform.forward * 10.5f;
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

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.skybox = null;

            // 灰盒 3D 舞台。玩家和道具站在 z = 0，地板顶面 y = 1.0（§4.2 地面线）。
            var stage = new GameObject("Stage").transform;
            Box("Floor", stage, new Vector3(5f, 0.5f, 1.5f), new Vector3(13f, 1f, 5f), LitMaterial("M_Floor", Floor));
            Box("FootLine", stage, new Vector3(5f, 0.995f, -0.99f), new Vector3(13f, 0.02f, 0.02f), LitMaterial("M_FootLine", FootLine));
            Box("Divider", stage, new Vector3(5f, 1.001f, 1.5f), new Vector3(0.03f, 0.002f, 5f), LitMaterial("M_Divider", Divider));
            Box("Backdrop", stage, new Vector3(5f, 3.6f, 4.0f), new Vector3(13f, 6f, 0.2f), LitMaterial("M_Wall", Wall));
            var wing = LitMaterial("M_Wing", Hex(0x232327));
            Box("Wing_L", stage, new Vector3(-0.1f, 3.0f, 2.2f), new Vector3(0.6f, 4.4f, 0.1f), wing);
            Box("Wing_R", stage, new Vector3(10.1f, 3.0f, 2.2f), new Vector3(0.6f, 4.4f, 0.1f), wing);
            Box("Valance", stage, new Vector3(5f, 5.15f, -0.6f), new Vector3(13f, 0.9f, 0.1f), wing);   // 檐幕，压住布景天顶以上

            // 追光：从正上方往下打的圆锥。父物体只沿 x 平移（Spotlight.cs），
            // 子物体一盏朝下的 Spot Light 负责照亮，一个半透明截头圆锥负责画出光柱。
            var lights = new GameObject("Spotlights").transform;
            var spotGo = new GameObject("Light_Main");
            spotGo.transform.SetParent(lights, false);
            spotGo.transform.position = new Vector3(2.5f, 10.6f, 0f);

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(spotGo.transform, false);
            beamGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = SpotYellow;
            beam.intensity = 220f;
            beam.range = 20f;
            beam.spotAngle = 30f;       // 地面光圈半径 ≈ 2.5，和光柱底面一致
            beam.innerSpotAngle = 28f;
            beam.shadows = LightShadows.Hard;

            var coneGo = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            coneGo.transform.SetParent(spotGo.transform, false);
            var coneRenderer = coneGo.GetComponent<MeshRenderer>();
            coneRenderer.sharedMaterial = ConeMaterial();
            coneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coneRenderer.receiveShadows = false;
            coneRenderer.sortingLayerName = "BG";   // 画在剪影后面，剪影保持纯黑
            coneGo.AddComponent<LightCone>();

            var spotlight = spotGo.AddComponent<Spotlight>();

            var props = new GameObject("Props").transform;

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0.8f, 1.0f, 0f);
            playerGo.transform.localScale = new Vector3(1.0f, 3.0f, 1f);   // TODO: 换成 128×192 的玩家帧
            AddSprite(playerGo, bottomSprite, Silhouette, "Player", 0);
            playerGo.AddComponent<SilhouetteShadow>().shadowMaterial = shadowCaster;
            playerGo.AddComponent<FrameAnim>();
            var player = playerGo.AddComponent<PlayerController>();
            player.spotlight = spotlight;

            BuildAudience();

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

        // 台前观众：一排黑色剪影的后脑勺和肩膀，压在画面底边，比舞台离相机更近。
        static void BuildAudience()
        {
            var root = new GameObject("Audience").transform;
            var rng = new System.Random(7);
            float x = 0.2f;
            int i = 0;
            while (x < 10.4f)
            {
                float jitter = (float)rng.NextDouble();
                float headR = 0.21f + 0.06f * jitter;
                float top = 0.85f + 0.2f * (float)rng.NextDouble();
                var person = new GameObject($"Seat{++i}").transform;
                person.SetParent(root, false);
                person.position = new Vector3(x, 0f, -2f);

                var shoulders = new GameObject("Shoulders");
                shoulders.transform.SetParent(person, false);
                shoulders.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                shoulders.transform.localScale = new Vector3(0.85f + 0.2f * jitter, top - 2f * headR + 0.45f, 1f);
                AddSprite(shoulders, bottomSprite, Silhouette, "Audience", 0);

                var head = new GameObject("Head");
                head.transform.SetParent(person, false);
                head.transform.localPosition = new Vector3(0.03f * (jitter - 0.5f), top - headR, 0f);
                head.transform.localScale = new Vector3(2f * headR, 2f * headR * 1.1f, 1f);
                AddSprite(head, circleSprite, Silhouette, "Audience", 1);

                x += 0.95f + 0.35f * (float)rng.NextDouble();
            }
        }

        static void EnsureCircleSprite()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(CirclePath))
                if (o is Sprite s) { circleSprite = s; return; }

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Circle", filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                    float a = Mathf.Clamp01(n / 2f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * n + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, CirclePath);
            circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            circleSprite.name = "circle";
            AssetDatabase.AddObjectToAsset(circleSprite, tex);
            AssetDatabase.SaveAssets();
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

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
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
