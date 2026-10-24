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

        [MenuItem("Sleight/兔子魔术/重建第一关场景")]
        public static void FromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (File.Exists(ScenePath) && !File.Exists("Assets/Scenes/LegacyGame.unity"))
                File.Copy(ScenePath, "Assets/Scenes/LegacyGame.unity");
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
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
            var root = new GameObject("RabbitTrick_FirstAct");
            var game = root.AddComponent<RabbitTrickGame>();
            var view = root.AddComponent<RabbitStageView>();
            var light = root.AddComponent<RandomAudienceLight>();
            var hands = root.AddComponent<DualHandController>();
            var audio = root.AddComponent<AudioSource>();
            var curtains = root.AddComponent<StageCurtains>();
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

        public static void BuildAndValidate()
        {
            ValidateRules();
            Build();
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            Expect(Vector2.Distance(game.transform.Find("Stage/RabbitBox/BoxDragHandle").position, game.BoxHandle) < 0.001f, "box handle artwork matches interaction position");
            Expect(Vector2.Distance(game.transform.Find("Stage/RabbitBox/LidHinge/LidHandle").position, game.LidHandle) < 0.001f, "lid handle artwork matches interaction position");
            ValidateHandsAndFullTrick(game);
            game.ResetTrick();
            game.view.UseRuntimeFont();
            game.view.Draw(game);
            Canvas.ForceUpdateCanvases();
            CapturePreview();
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath,
                "PASS: ordered three-step trick; continuous empty-box demonstration; dark-only insertion; " +
                "carried and inserted rabbit exposure; closed-box safety; final reveal; reset; independent hands; " +
                "left-to-right handoff; physical lid dragging; physical box dragging; dancer activation and light lock; " +
                "full trick via body walking and short articulated arms; moving decoy occlusion; lit-only unobstructed presentation; shader and scene references.\n");
            Debug.Log("[RabbitTrick] ALL VALIDATION PASSED. " + ReportPath);
            game.ResetTrick();
            // Do not serialize test positions or machine-specific runtime fonts into the scene.
        }

        static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Rabbit trick validation: " + message);
        }

        static RabbitTrickRules EmptyShown()
        {
            var rules = new RabbitTrickRules();
            rules.Reset();
            rules.ShowEmpty(0.9f, true, true);
            Expect(rules.Step == TrickStep.HideRabbit, "lit empty display must advance");
            return rules;
        }

        static void ValidateRules()
        {
            var rules = new RabbitTrickRules();
            rules.Reset();
            Expect(!rules.TryInsert(true, false, false), "cannot skip empty display");
            rules.ShowEmpty(0.5f, true, true);
            rules.ShowEmpty(0.5f, true, false);
            Expect(rules.Step == TrickStep.ShowEmpty && rules.EmptyShowProgress == 0, "empty display must be continuous and lit");
            rules = EmptyShown();
            rules.TouchRabbit();
            rules.CheckExposure(true, false);
            Expect(rules.Failed, "carried rabbit in light must fail");
            rules = EmptyShown();
            Expect(!rules.TryInsert(true, false, true) && rules.Failed, "lit insertion must fail");
            rules = EmptyShown();
            Expect(!rules.TryInsert(false, false, false), "closed lid blocks insertion");
            Expect(rules.TryInsert(true, false, false), "dark insertion must work");
            rules.CheckExposure(true, true);
            Expect(rules.Failed, "unclosed loaded box exposes the secret");
            rules = EmptyShown();
            rules.TryInsert(true, false, false);
            Expect(!rules.TryReveal(true, true), "must close the box before final reveal");
            rules.CloseBox(true);
            rules.CheckExposure(true, false);
            Expect(!rules.Failed, "closed loaded box may be illuminated");
            rules.CheckExposure(true, true);
            Expect(!rules.Failed, "final opening is a legitimate reveal");
            Expect(!rules.TryReveal(true, false), "audience must see final reveal");
            Expect(rules.TryReveal(true, true) && rules.Finished, "final reveal must complete");
            rules.Reset();
            Expect(!rules.Failed && !rules.RabbitInside && !rules.RabbitTouched && rules.Step == TrickStep.ShowEmpty, "retry resets all rules");
        }

        static void ValidateHandsAndFullTrick(RabbitTrickGame game)
        {
            game.ResetTrick();
            game.Hands.SelectHand(1);
            WalkTo(game, 5.1f);
            Aim(game, game.LidHandle);
            game.Hands.Interact(game);
            Expect(game.Hands.Selected.holding == HandItem.Lid, "nearby hand grabs lid");
            game.Hands.Move(game, Vector2.up * 0.75f);
            Expect(game.LidAmount > 0.99f, "fixed arm can open lid");
            game.Hands.Interact(game);
            game.Evaluate(0.9f);
            Expect(game.Rules.Step == TrickStep.HideRabbit, "lit empty-box display advances");

            WalkTo(game, 8.5f);
            Aim(game, game.DancerHandle);
            game.Hands.Interact(game);
            game.Hands.Move(game, Vector2.up * 0.3f);
            Expect(game.Audience.Dancing, "hand pull starts dancing");
            game.Audience.Tick(1f);
            Expect(Mathf.Abs(game.Audience.X - game.Audience.DancerX) < 0.1f, "spotlight follows moving decoy");
            WalkTo(game, 1.55f);
            game.Hands.SelectHand(0);
            Aim(game, game.RabbitPosition);
            game.Hands.Interact(game);
            Expect(game.Hands.Selected.holding == HandItem.Rabbit, "left hand picks up rabbit within reach");
            game.Hands.SelectHand(1);
            Aim(game, game.RabbitPosition);
            game.Hands.Interact(game);
            Expect(game.Hands.Selected.holding == HandItem.Rabbit && game.Hands.Other.holding == HandItem.None, "left-to-right handoff");
            WalkTo(game, 5.1f);
            MoveTo(game, game.BoxOpening + Vector2.up * 0.12f);
            game.Hands.Interact(game);
            Expect(game.Rules.RabbitInside && !game.Rules.Failed, "walking and carrying to dark box works");
            MoveTo(game, game.LidHandle);
            game.Hands.Interact(game);
            game.Hands.Move(game, Vector2.down * 0.75f);
            game.Hands.Interact(game);
            game.Evaluate(0.02f);
            Expect(game.Rules.Step == TrickStep.RevealRabbit, "close loaded box to arm reveal");
            WalkTo(game, 5.25f);
            Aim(game, game.BoxHandle);
            game.Hands.Interact(game);
            Expect(game.Hands.Selected.holding == HandItem.Box, "short arm can grab box face");
            float oldX = game.BoxX;
            game.Hands.Move(game, Vector2.left * 0.2f);
            Expect(game.BoxX < oldX, "hand drags box");
            game.Hands.Interact(game);
            game.Audience.ResetLight(game.BoxX);
            Aim(game, game.LidHandle);
            game.Hands.Interact(game);
            game.Hands.Move(game, Vector2.up * 0.75f);
            game.Evaluate(0.02f);
            Expect(game.Rules.Finished && !game.Rules.Failed, "whole trick succeeds with fixed-length arms");

            game.ResetTrick();
            for (int i = 0; i < 2; i++)
            {
                game.Hands.SelectHand(i);
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * Mathf.PI / 8f;
                    game.Hands.Move(game, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f);
                    Vector2 shoulder = game.Hands.Shoulder(i), elbow = game.Hands.Elbow(i), hand = game.Hands.Hands[i].position;
                    Expect(Mathf.Abs(Vector2.Distance(shoulder, elbow) - game.Hands.upperArm) < 0.002f, "upper arm never stretches");
                    Expect(Mathf.Abs(Vector2.Distance(elbow, hand) - game.Hands.forearm) < 0.002f, "forearm never stretches");
                    Expect(Vector2.Distance(shoulder, hand) <= game.Hands.Reach + 0.001f, "hand stays in reach");
                }
            }
            game.ResetTrick();
            game.Audience.ResetLight(1.3f);
            game.SetLid(1);
            game.Evaluate(1f);
            Expect(game.Rules.Step == TrickStep.ShowEmpty, "empty display in darkness does not count");

            // An opaque MOVING decoy protects a rabbit only while its complete silhouette stays behind the cape.
            game.ResetTrick();
            game.Rules.ShowEmpty(1f, true, true);
            game.Rules.TouchRabbit();
            game.Audience.Distract(5.6f);
            game.Audience.SetDancerX(5.6f);
            game.Audience.Tick(1f);
            for (int i = 0; i < 5; i++)
            {
                game.Audience.SetDancerX(5.6f + i * 0.1f);
                game.Audience.Tick(0.1f);
                game.RabbitPosition = new Vector2(game.Audience.DancerX, 1.9f);
                Expect(game.Audience.Illuminates(game.RabbitPosition), "covered route is actually lit");
                game.Evaluate(0.1f);
                Expect(!game.Rules.Failed, "moving cape protects rabbit in the light");
            }
            game.RabbitPosition += Vector2.left * 0.65f;
            game.Evaluate(0.01f);
            Expect(game.Rules.Failed, "rabbit leaving cape is immediately exposed");

            // Loading in light is legal behind the cape; presentation must be visible and lit.
            game.ResetTrick();
            game.Rules.ShowEmpty(1f, true, true);
            game.SetLid(1f);
            game.Audience.Distract(game.BoxX);
            game.Audience.SetDancerX(game.BoxX);
            game.Audience.Tick(1f);
            game.RabbitPosition = game.BoxOpening;
            Expect(game.TryPutRabbit(), "cape permits concealed loading inside spotlight");
            game.Evaluate(0.1f);
            Expect(!game.Rules.Failed, "cape protects loaded open box");
            game.SetLid(0f);
            game.SetLid(1f);
            game.Evaluate(0.1f);
            Expect(!game.Rules.Finished, "cannot present through cape");
            game.Audience.SetDancerX(game.BoxX + 0.95f);
            game.Audience.Tick(1f);
            game.Evaluate(0.1f);
            Expect(game.Rules.Finished, "lit unobstructed final reveal succeeds");
            game.ResetTrick();
        }

        static void WalkTo(RabbitTrickGame game, float x)
        {
            for (int i = 0; i < 500 && Mathf.Abs(game.Hands.BodyPosition.x - x) > 0.01f; i++)
            {
                game.Hands.MoveBody(game, Vector2.right * Mathf.Clamp(x - game.Hands.BodyPosition.x, -0.045f, 0.045f));
                game.Audience.Tick(0.02f);
                game.Evaluate(0.02f);
                Expect(!game.Rules.Failed, "walking path stays safe");
            }
            Expect(Mathf.Abs(game.Hands.BodyPosition.x - x) <= 0.02f, "body reaches walking target");
        }
        static void Aim(RabbitTrickGame game, Vector2 point) => game.Hands.Selected.position = game.Hands.ClampHand(game.Hands.Active, point);
        static void MoveTo(RabbitTrickGame game, Vector2 target)
        {
            for (int i = 0; i < 300 && Vector2.Distance(game.Hands.Selected.position, target) > 0.015f; i++)
            {
                game.Hands.Move(game, Vector2.ClampMagnitude(target - game.Hands.Selected.position, 0.04f));
                game.Audience.Tick(0.016f);
                game.Evaluate(0.016f);
                Expect(!game.Rules.Failed, "hand transfer stays concealed");
            }
            Expect(Vector2.Distance(game.Hands.Selected.position, target) < 0.03f, "hand reaches target without stretching");
        }
        static void CapturePreview()
        {
            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/RabbitTrickPreview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
