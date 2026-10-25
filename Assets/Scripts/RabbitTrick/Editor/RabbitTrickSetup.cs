using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Sleight.RabbitTrick.EditorTools
{
    public static class RabbitTrickSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string ArtPath = "Assets/Art/RabbitTrick";
        const string ReportPath = "Logs/RabbitTrickValidation.txt";

        [MenuItem("Sleight/Stage Show/Rebuild Ten-Act Stage")]
        public static void FromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // Debug：运行中从编辑器菜单直接跳关（也可以在游戏里按 F10）
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 1 关")] static void Jump1() => Jump(0);
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 2 关")] static void Jump2() => Jump(1);
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 3 关")] static void Jump3() => Jump(2);
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 4 关")] static void Jump4() => Jump(3);
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 5 关")] static void Jump5() => Jump(4);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 06")] static void Jump6() => Jump(5);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 07")] static void Jump7() => Jump(6);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 08")] static void Jump8() => Jump(7);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 09")] static void Jump9() => Jump(8);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 10")] static void Jump10() => Jump(9);
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 06", true)]
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 07", true)]
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 08", true)]
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 09", true)]
        [MenuItem("Sleight/Stage Show/Debug/Jump to Trick 10", true)]
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 1 关", true)]
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 2 关", true)]
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 3 关", true)]
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 4 关", true)]
        [MenuItem("Sleight/兔子魔术/Debug/跳到第 5 关", true)]
        static bool CanJump() => Application.isPlaying;

        // 快进跑一遍三关的开场演示，确认每段都能把魔术演成功（批处理：-executeMethod ...ValidateDemos）
        [MenuItem("Sleight/Stage Show/Debug/Validate All Ten Demos (rebuilds scene)")]
        public static void ValidateDemos() => CampaignValidation.BuildAndValidate();
        static void Jump(int level)
        {
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game != null) game.JumpToLevel(level);
        }

        public static void Build()
        {
            if (File.Exists(ScenePath) && !File.Exists("Assets/Scenes/LegacyGame.unity"))
                File.Copy(ScenePath, "Assets/Scenes/LegacyGame.unity");
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            ConfigureAudio();
            var shader = Shader.Find("Sleight/Rabbit Stage");
            if (shader == null) throw new InvalidOperationException("Rabbit Stage shader was not imported.");
            var stage = Material("Stage", shader, 0, 0);
            var magician = Material("Magician", shader, 1, 0);
            magician.SetFloat("_HideLegs", 1f);
            var overlay = Material("Overlay", shader, 0, 1);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(5, 2.8125f, -10);
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.8125f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(18, 14, 26, 255);
            camera.allowHDR = false;
            var root = new GameObject("TenTrickShow");
            var game = root.AddComponent<RabbitTrickGame>();
            var view = root.AddComponent<RabbitStageView>();
            var light = root.AddComponent<RandomAudienceLight>();
            var hands = root.AddComponent<DualHandController>();
            var audio = root.AddComponent<AudioSource>();
            var curtains = root.AddComponent<StageCurtains>();
            root.AddComponent<TrickDebugMenu>().game = game;
            game.Curtains = curtains;
            view.curtains = curtains;
            audio.playOnAwake = false;
            game.view = view;
            game.Audience = light;
            game.Hands = hands;
            game.audioSource = audio;
            view.square = CenteredSprite("Assets/Art/Placeholder/Placeholder.asset", "Square");
            view.circle = CenteredSprite("Assets/Art/Placeholder/Circle.asset", "Circle");
            view.magicianArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/player_idle_00.png");
            if (view.magicianArt == null || view.square == null || view.circle == null)
                throw new InvalidOperationException("Required existing art is missing.");
            view.audienceArt = new Sprite[6];
            for (int i = 0; i < view.audienceArt.Length; i++)
                view.audienceArt[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/audience_{i:00}.png");
            view.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/fx_glow.png");
            view.flameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/fx_flame.png");
            view.audienceBodies = new Sprite[7];
            view.audienceHeads = new Sprite[7];
            for (int i = 0; i < 7; i++)
            {
                view.audienceBodies[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/audience_body_{i:00}.png");
                view.audienceHeads[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/audience_head_{i:00}.png");
            }
            view.stageMaterial = stage;
            view.magicianMaterial = magician;
            view.overlayMaterial = overlay;
            view.lineMaterial = Material("Line", shader, 0, 0);
            view.lineOverlayMaterial = Material("LineOverlay", shader, 0, 1);
            view.interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.Build();
            game.ResetTrick();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            AssetDatabase.SaveAssets();
            Debug.Log("[RabbitTrick] New first act saved to Assets/Scenes/Game.unity. Previous scene retained as LegacyGame.unity.");
        }

        static void ConfigureAudio()
        {
            const string folder = "Assets/Resources/StageAudio";
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*.ogg"))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                var loadType = path.Contains("TheatreApplause") ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
                if (settings.loadType == loadType && Mathf.Abs(settings.quality - 0.7f) < 0.01f) continue;
                settings.loadType = loadType; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = 0.7f;
                importer.defaultSampleSettings = settings; importer.forceToMono = false;
                importer.SaveAndReimport();
            }
        }

        static Material Material(string name, Shader shader, float magician, float unlit)
        {
            string path = $"{ArtPath}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetFloat("_Magician", magician);
            material.SetFloat("_Unlit", unlit);
            material.SetFloat("_UseSpriteColor", name.StartsWith("Line") ? 0f : 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Sprite CenteredSprite(string sourcePath, string name)
        {
            string path = $"{ArtPath}/{name}.asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            var source = AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing shape sprite: " + sourcePath);
            sprite = Sprite.Create(source.texture, source.rect, Vector2.one * 0.5f, source.pixelsPerUnit);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        public static void BuildAndValidate() => CampaignValidation.BuildAndValidate();
    }
}
