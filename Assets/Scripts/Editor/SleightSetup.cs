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
        const string PrefabDir = "Assets/Prefabs";
        const string SpriteDir = "Assets/Art/Sprites";
        const string PostFxPath = "Assets/Settings/PostFX_Sleight.asset";
        const string PipelinePath = "Assets/Settings/UniversalRP_3D.asset";
        const string RendererPath = "Assets/Settings/UniversalRenderer_3D.asset";
        const string MaterialDir = "Assets/Art/Materials";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static readonly string[] SortingLayers = { "BG", "Stage", "Props", "Player", "Light", "HUD", "Audience" };

        // 团队定稿的调色板（取代 GDD §5.1 的灰色系）：近黑、暖棕、旧纸、深红，黄色只给追光。
        static readonly Color Silhouette = Hex(0x0E0B0A);
        static readonly Color Wall = Hex(0x1A1412);
        static readonly Color Floor = Hex(0x4A3426);
        static readonly Color FootLine = Hex(0x6B4E3A);
        static readonly Color Divider = Hex(0x2A1E17);
        static readonly Color Crimson = Hex(0x5A1418);
        static readonly Color AgedPaper = Hex(0xD9CBB0);
        static readonly Color SpotYellow = Hex(0xFFD21E);
        static readonly Color Ambient = Hex(0x4A3E36);   // 暗区要暗，追光才显眼；偏暖
        static readonly Color ClearColor = Hex(0x0E0B0A);

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
            shadowCaster = ShadowMaterial();
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

        // 只投影子的材质：按剪影贴图的 alpha 裁剪，影子形状和剪影一致（SilhouetteShadow 每帧换贴图）。
        static Material ShadowMaterial()
        {
            var mat = LitMaterial("M_ShadowCaster", Silhouette);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material DustMaterial()
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/M_Dust.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // 叠加
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (circleSprite != null) mat.SetTexture("_BaseMap", circleSprite.texture);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        static Sprite[] ArtFrames(string prefix, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Art($"{prefix}_{i:00}");
                if (frames[i] == null) return null;
            }
            return frames;
        }

        static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }

        // §18.3：按类型的道具预制体。缺图时返回 null，运行时退回色块占位 P_Prop。
        static PropInteractable EnsureTypedPropPrefab(string name, string type, string wrong, string right)
        {
            var a = Art(wrong);
            var b = Art(right);
            if (a == null || b == null) return null;
            var go = new GameObject(name);
            AddSprite(go, a, Color.white, "Props", 0);
            var prop = go.AddComponent<PropInteractable>();
            prop.type = type;
            prop.stateSprites = new[] { a, b };
            go.AddComponent<SilhouetteShadow>().shadowMaterial = shadowCaster;
            return SavePrefab<PropInteractable>(go, name);
        }

        static GameObject EnsureMarkerPrefab()
        {
            var sprite = Art("prop_marker_assist");
            if (sprite == null) return null;
            var go = new GameObject("P_Marker_Assist");
            AddSprite(go, sprite, Color.white, "Stage", 0);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 平铺在地板上
            go.transform.localScale = new Vector3(1f, 3f, 1f);        // 相机俯角小，纵向拉长才看得见
            return SavePrefab<Transform>(go, "P_Marker_Assist").gameObject;
        }

        static SpriteRenderer EnsureEyePrefab()
        {
            var sprite = Art("eye_flash");
            if (sprite == null) return null;
            var go = new GameObject("P_EyeFlash");
            AddSprite(go, sprite, Color.white, "Light", 10);
            return SavePrefab<SpriteRenderer>(go, "P_EyeFlash");
        }

        static PlayerController EnsurePlayerPrefab()
        {
            var go = new GameObject("P_Player");
            var idle = ArtFrames("player_idle", 2);
            if (idle != null)
            {
                AddSprite(go, idle[0], Color.white, "Player", 0);
            }
            else
            {
                AddSprite(go, bottomSprite, Silhouette, "Player", 0);
                go.transform.localScale = new Vector3(1.0f, 3.0f, 1f);
            }
            go.AddComponent<SilhouetteShadow>().shadowMaterial = shadowCaster;
            go.AddComponent<FrameAnim>();
            var player = go.AddComponent<PlayerController>();
            player.idleFrames = idle;
            player.walkFrames = ArtFrames("player_walk", 4);
            player.actFrames = ArtFrames("player_act", 2);
            player.performFrames = ArtFrames("player_perform", 3);
            player.caughtFrame = Art("player_caught_00");
            return SavePrefab<PlayerController>(go, "P_Player");
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
            // 深红丝绒：台口两侧的大幕、后面一层侧幕、顶上的檐幕。
            var velvet = LitMaterial("M_Velvet", Crimson);
            Box("Drape_L", stage, new Vector3(-0.05f, 3.0f, -0.8f), new Vector3(0.8f, 4.8f, 0.1f), velvet);
            Box("Drape_R", stage, new Vector3(10.05f, 3.0f, -0.8f), new Vector3(0.8f, 4.8f, 0.1f), velvet);
            Box("Wing_L", stage, new Vector3(0.1f, 3.0f, 2.2f), new Vector3(0.9f, 4.6f, 0.1f), velvet);
            Box("Wing_R", stage, new Vector3(9.9f, 3.0f, 2.2f), new Vector3(0.9f, 4.6f, 0.1f), velvet);
            Box("Valance", stage, new Vector3(5f, 5.15f, -0.6f), new Vector3(13f, 0.9f, 0.1f), velvet);   // 檐幕，压住布景天顶以上

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
            BuildDust(spotGo.transform);

            var spotlight = spotGo.AddComponent<Spotlight>();

            var props = new GameObject("Props").transform;

            var playerPrefab = EnsurePlayerPrefab();
            var player = (PlayerController)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(0.8f, 1.0f, 0f);
            player.spotlight = spotlight;

            BuildAudience(spotGo.transform);
            BuildPostFx(camData);

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
            runner.boxPrefab = EnsureTypedPropPrefab("P_Prop_Box", "box", "prop_box_open", "prop_box_sealed");
            runner.clothPrefab = EnsureTypedPropPrefab("P_Prop_Cloth", "cloth", "prop_cloth_off", "prop_cloth_on");
            runner.mirrorPrefab = EnsureTypedPropPrefab("P_Prop_Mirror", "mirror", "prop_mirror_normal", "prop_mirror_flipped");
            runner.markerPrefab = EnsureMarkerPrefab();
            runner.eyePrefab = EnsureEyePrefab();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // 台前观众：一排黑色剪影的后脑勺和肩膀，压在画面底边，比舞台离相机更近。
        // 前排正中偏右坐着"他"，两只小眼睛反着光，跟着追光转（WatcherEyes）。
        static void BuildAudience(Transform spotlight)
        {
            var root = new GameObject("Audience").transform;
            var rng = new System.Random(7);
            var kinds = new Sprite[6];
            for (int k = 0; k < kinds.Length; k++) kinds[k] = Art($"audience_{k:00}");
            var watcherSprite = Art("audience_watcher");
            var eyesSprite = Art("watcher_eyes");
            bool hasArt = kinds[0] != null && watcherSprite != null;

            float x = 0.2f;
            int i = 0;
            bool watcherPlaced = false;
            while (x < 10.4f)
            {
                var person = new GameObject($"Seat{++i}").transform;
                person.SetParent(root, false);
                bool isWatcher = !watcherPlaced && x > 5.4f;

                if (hasArt)
                {
                    person.position = new Vector3(x, -0.05f - 0.12f * (float)rng.NextDouble(), -2f);
                    var sprite = isWatcher ? watcherSprite : kinds[rng.Next(kinds.Length)];
                    var sr = AddSprite(person.gameObject, sprite, Color.white, "Audience", 0);
                    sr.flipX = rng.Next(2) == 0;
                    if (isWatcher && eyesSprite != null)
                    {
                        person.name = "Watcher";
                        var eyes = new GameObject("Eyes");
                        eyes.transform.SetParent(person, false);
                        eyes.transform.localPosition = new Vector3(0f, (96f - 44f) / 64f, 0f);
                        AddSprite(eyes, eyesSprite, Color.white, "Audience", 2);
                        eyes.AddComponent<WatcherEyes>().spotlight = spotlight;
                    }
                }
                else
                {
                    float jitter = (float)rng.NextDouble();
                    float headR = 0.21f + 0.06f * jitter;
                    float top = 0.85f + 0.2f * (float)rng.NextDouble();
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
                }

                watcherPlaced |= isWatcher;
                x += 0.95f + 0.35f * (float)rng.NextDouble();
            }
        }

        // 光柱里的浮尘：跟着追光走，叠加的旧纸色小点。
        static void BuildDust(Transform spot)
        {
            var go = new GameObject("Dust", typeof(ParticleSystem));
            go.transform.SetParent(spot, false);
            go.transform.position = new Vector3(spot.position.x, 3.6f, 0f);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.03f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new Color(AgedPaper.r, AgedPaper.g, AgedPaper.b, 0.55f);
            main.maxParticles = 150;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 20f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.4f, 4.6f, 2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.25f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DustMaterial();
            r.sortingLayerName = "Light";
        }

        // 1930 年代电影海报的质感：胶片颗粒、轻微暗角、偏暖偏旧的调色，追光带一点泛光。
        static void BuildPostFx(UniversalAdditionalCameraData camData)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath) != null) AssetDatabase.DeleteAsset(PostFxPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxPath);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.45f);
            grain.response.Override(0.8f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-15f);
            color.colorFilter.Override(new Color(1f, 0.95f, 0.88f));
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostFX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            camData.renderPostProcessing = true;
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
                var dot = UiImage($"Dot{i + 1}", dots, new Color(AgedPaper.r, AgedPaper.g, AgedPaper.b, 0.25f));
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(10, 10);
                rt.anchoredPosition = new Vector2(20 + i * 22, -330 + 10);
                hud.actDots[i] = dot;
            }

            hud.flashWhite = FullScreen("FlashWhite", canvasGo.transform, new Color(1, 1, 1, 0));
            hud.fadeBlack = FullScreen("FadeBlack", canvasGo.transform, new Color(0, 0, 0, 0));

            var title = Art("title_card");
            if (title != null)
            {
                hud.titleCard = FullScreen("TitleCard", canvasGo.transform, Color.white);
                hud.titleCard.sprite = title;
                hud.titleCard.preserveAspect = true;
                hud.titleCard.gameObject.SetActive(false);
            }
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
