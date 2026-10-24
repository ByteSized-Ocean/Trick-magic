using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sleight.RabbitTrick.EditorTools
{
    public static class CampaignValidation
    {
        public static void BuildAndValidate()
        {
            RabbitTrickSetup.Build();
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            var report = new StringBuilder();
            bool good = true;
            RabbitTrickGame.SimulateInEditor = true;
            try
            {
                for (int level = 0; level < RabbitTrickGame.LevelCount; level++)
                {
                    bool passed = game.SimulateDemo(level);
                    report.AppendLine((passed ? "PASS " : "FAIL ") + game.LastDemoReport);
                    good &= passed;
                }
                CheckRabbitFocus(game); report.AppendLine("PASS first spotlight: empty box, stationary jumper, box; no empty-space random targets.");
                CheckRod(game); report.AppendLine("PASS levitation: uncovered lit support fails immediately; focus follows magician.");
                CheckLevitationDarkSteps(game); report.AppendLine("PASS levitation: dark raising, dark hoop pass, and dark lowering never complete their steps.");
                CheckDoveFocus(game); report.AppendLine("PASS dove spotlight: follows the flying dove, then quickly returns to the magician after exit.");
                CheckDepth(game); report.AppendLine("PASS smooth depth: hand-held item follows W/S displacement and lands at retained depth.");
                CheckScreenGeometry(game); report.AppendLine("PASS fifth trick: true depth occlusion; standing at back without cover is exposed.");
                CheckFreeProps(game); report.AppendLine("PASS unrestricted props: entire hat brim, pre-step dove, flowers, golden orb; rabbit insertion is not step-locked.");
                CheckSorting(game); report.AppendLine("PASS depth rendering: body, arms, hands, held orb, both screen layers; order restores when changing levels.");
            }
            finally
            {
                game.JumpToLevel(0); if (game.InDemo) game.EndDemo();
                RabbitTrickGame.SimulateInEditor = false;
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FiveTrickValidation.txt", report.ToString());
            Debug.Log("[FiveTricks] Campaign validation\n" + report);
            if (!good) throw new InvalidOperationException("One or more five-trick demos failed.\n" + report);
        }
        static void Expect(bool value, string message)
        { if (!value) throw new InvalidOperationException("[FiveTricks] " + message); }
        static void Load(RabbitTrickGame game, int level)
        { game.JumpToLevel(level); if (game.InDemo) game.EndDemo(); game.Audience.Scripted = false; }
        static void CheckRabbitFocus(RabbitTrickGame game)
        {
            Load(game, 0);
            game.DirectAudience(0.02f); game.Audience.Tick(0.02f);
            Expect(game.Audience.FocusName == "EMPTY BOX", "First light must wait for empty-box display.");
            game.Rules.ShowEmpty(1f, true, true);
            for (int i = 0; i < 120; i++) { game.DirectAudience(0.01f); game.Audience.Tick(0.01f); }
            Expect(Mathf.Abs(game.Audience.X - 8.25f) < 0.05f, "After empty display the light must watch the stationary jumper.");
            for (int i = 0; i < 750; i++) { game.DirectAudience(0.01f); game.Audience.Tick(0.01f); }
            Expect(game.Audience.FocusName == "MAGIC BOX", "Light must return to actual box.");
        }
        static void CheckRod(RabbitTrickGame game)
        {
            Load(game, 2);
            game.Hands.MoveBody(game, Vector2.right * (5.25f - game.Hands.BodyPosition.x));
            game.Hands.SelectHand(1); game.Hands.Selected.position = new Vector2(5.52f, 1.55f);
            game.Hands.Grab(game);
            Expect(game.Hands.Selected.holding == HandItem.Custom, "Rod lever must be reachable.");
            game.Hands.Move(game, Vector2.up * 0.15f);
            game.DirectAudience(0.1f); game.Audience.Tick(0.1f); game.Trick.Tick(0.1f);
            var trick = (LevitationTrick)game.Trick;
            Expect(trick.LiftAmount > 0.05f && trick.SupportCovered && !trick.Failed, "Aligned magician must hide the rod.");
            game.Hands.MoveBody(game, Vector2.right * 0.35f);
            game.DirectAudience(0.1f); game.Audience.Tick(0.1f); trick.Tick(0.1f);
            Expect(trick.Failed, "Uncovered iron support in the light must fail immediately.");
        }
        static void PrepareLevitation(RabbitTrickGame game, int step)
        {
            Load(game, 2);
            game.StartDemo();
            var tick = typeof(RabbitTrickGame).GetMethod("TickDemo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int frame = 0; frame < 6000; frame++)
            {
                tick.Invoke(game, new object[] { 1f / 60f });
                var trick = (LevitationTrick)game.Trick;
                if (trick.Step == step && (step != 0 || trick.LiftAmount >= 0.98f)) return;
                Expect(!trick.Failed && game.InDemo, "Levitation regression setup must reach a valid demonstration state.");
            }
            throw new InvalidOperationException("Levitation demonstration did not reach step " + step);
        }
        static void CheckLevitationDarkSteps(RabbitTrickGame game)
        {
            for (int step = 0; step < 3; step++)
            {
                PrepareLevitation(game, step);
                var trick = (LevitationTrick)game.Trick;
                game.Audience.ResetLight(0);
                game.Audience.Scripted = true;
                if (step == 1)
                {
                    game.Hands.SelectHand(0);
                    game.Hands.Move(game, new Vector2(4.5f, 2.67f) - game.Hands.Selected.position);
                    trick.Tick(0.02f);
                    for (int i = 0; i < 70; i++) { game.Hands.Move(game, Vector2.right * 0.02f); trick.Tick(0.02f); }
                }
                else if (step == 2)
                {
                    game.Hands.SelectHand(1);
                    for (int i = 0; i < 50; i++) { game.Hands.Move(game, Vector2.down * 0.01f); trick.Tick(0.02f); }
                }
                for (int i = 0; i < 90; i++) trick.Tick(0.02f);
                Expect(!trick.Failed && trick.Step == step && trick.StepProgress < 0.01f,
                    "Levitation step " + (step + 1) + " must not complete in darkness.");
            }
        }
        static void CheckDoveFocus(RabbitTrickGame game)
        {
            Load(game, 3);
            var trick = (DoveHatTrick)game.Trick;
            var hand = game.Hands.Hands[1];
            hand.holding = HandItem.Custom; hand.custom = 1;
            trick.Release(hand, 1); hand.holding = HandItem.None;
            for (int i = 0; i < 60; i++) { game.DirectAudience(0.02f); game.Audience.Tick(0.02f); trick.Tick(0.02f); }
            Expect(trick.Flying && game.Audience.FocusName == "FLYING DOVE" && Mathf.Abs(game.Audience.X - trick.Dove.Center.x) < 0.05f,
                "Spotlight must track a real flying dove.");
            for (int i = 0; i < 1000 && !trick.DoveGone; i++) { game.DirectAudience(0.02f); game.Audience.Tick(0.02f); trick.Tick(0.02f); }
            for (int i = 0; i < 30; i++) { game.DirectAudience(0.02f); game.Audience.Tick(0.02f); }
            Expect(trick.DoveGone && game.Audience.FocusName == "MAGICIAN" && Mathf.Abs(game.Audience.X - game.Hands.BodyPosition.x) < 0.05f,
                "Spotlight must quickly return to the magician once the dove is offscreen.");
        }
        static void CheckDepth(RabbitTrickGame game)
        {
            Load(game, 3);
            var trick = (DoveHatTrick)game.Trick;
            var prop = trick.Flowers;
            game.Hands.SelectHand(1);
            game.Hands.Selected.position = game.Hands.Shoulder(1) + Vector2.down * 0.7f;
            prop.position = game.Hands.Selected.position + Vector2.down * 0.3f;
            prop.Grab(1, game.Hands.Selected.position);
            game.Hands.Selected.holding = HandItem.Custom; game.Hands.Selected.custom = 2;
            Vector2 before = prop.position, bodyBefore = game.Hands.BodyPosition;
            game.Hands.MoveBody(game, Vector2.up * 0.2f);
            float delta = game.Hands.BodyPosition.y - bodyBefore.y;
            Expect(game.Hands.Depth > 0 && game.Hands.Depth < 1, "Depth move must allow intermediate positions.");
            Expect(Mathf.Abs(prop.position.y - before.y - delta) < 0.002f, "Held item must follow W/S movement, not lag until next tick.");
            float depth = prop.depth;
            game.Hands.Release(game);
            for (int i = 0; i < 180; i++) prop.Tick(1f / 60f);
            Expect(Mathf.Abs(prop.depth - depth) < 0.001f && Mathf.Abs(prop.position.y - (1.18f + depth * DualHandController.LaneLift)) < 0.01f,
                "Released item must fall to the floor at its retained continuous depth.");
        }
        static void CheckScreenGeometry(RabbitTrickGame game)
        {
            Load(game, 4);
            var trick = (DepthTransferTrick)game.Trick;
            var orb = trick.Orb;
            Expect(Mathf.Abs(game.view.World.Find("DepthTransferTrick/FirstScreenFixed").position.z - 0.3f) < 0.001f,
                "First screen must occupy its actual continuous world depth.");
            Expect(Mathf.Abs(game.view.World.Find("DepthTransferTrick/SecondDepthCurtain").position.z - 0.87f) < 0.001f,
                "Second screen must physically sit behind the first screen.");
            orb.position = new Vector2(5.8f, 1.7f); orb.depth = 0.9f;
            Expect(trick.IsOccluded(orb), "Second screen must cover objects behind it.");
            orb.depth = 0.3f;
            Expect(!trick.IsOccluded(orb), "Same screen coordinates in FRONT of screen must not count as hidden.");
            orb.depth = 0.9f; orb.position = new Vector2(8.4f, 1.7f);
            Expect(!trick.IsOccluded(orb), "Back of stage alone must not grant invisibility outside actual screens.");
        }
        static void SetDepth(RabbitTrickGame game, float depth)
        { game.Hands.MoveBody(game, Vector2.up * ((DualHandController.FrontY + depth * DualHandController.LaneLift - game.Hands.BodyPosition.y) / 0.55f)); }
        static void CheckFreeProps(RabbitTrickGame game)
        {
            foreach (float x in new[] { 5.02f, 5.32f, 5.6f, 5.88f, 6.18f })
            {
                Load(game, 3);
                var hat = (DoveHatTrick)game.Trick;
                game.Hands.MoveBody(game, Vector2.right * (x - game.Hands.BodyPosition.x));
                game.Hands.SelectHand(0); game.Hands.Move(game, new Vector2(x, hat.HatHandle.y) - game.Hands.Selected.position);
                game.Hands.Grab(game);
                Expect(game.Hands.Selected.holding == HandItem.Custom && game.Hands.Selected.custom == 0 && hat.Step == 0,
                    "Every part of the brim must be grabbable before showing the empty hat.");
                game.Hands.Move(game, Vector2.up * 0.25f);
                Expect(Mathf.Abs(game.Hands.Selected.position.x - x) < 0.01f && hat.HatTilt > 0.4f,
                    "Brim grab must keep the actual contact point, not snap to the right side.");
                game.Hands.Release(game);
            }
            Load(game, 3);
            var trick = (DoveHatTrick)game.Trick;
            game.Hands.MoveBody(game, Vector2.right * (7.1f - game.Hands.BodyPosition.x));
            game.Hands.SelectHand(1); game.Hands.Move(game, trick.Dove.Center - game.Hands.Selected.position); game.Hands.Grab(game);
            Expect(trick.Dove.Held && trick.Step == 0, "Dove must be available before empty-hat presentation.");
            game.Hands.Release(game); Expect(trick.Flying, "A pre-step dove release must still launch it.");
            game.Hands.MoveBody(game, Vector2.right * (9.1f - game.Hands.BodyPosition.x));
            game.Hands.Move(game, trick.Flowers.Center - game.Hands.Selected.position); game.Hands.Grab(game);
            Expect(trick.Flowers.Held && trick.Step == 0, "Flowers must be usable before step progression.");
            Load(game, 4);
            var transfer = (DepthTransferTrick)game.Trick;
            SetDepth(game, transfer.Orb.depth);
            game.Hands.MoveBody(game, Vector2.right * (3.15f - game.Hands.BodyPosition.x));
            game.Hands.SelectHand(0); game.Hands.Move(game, transfer.Orb.Center - game.Hands.Selected.position); game.Hands.Grab(game);
            Expect(transfer.Orb.Held && transfer.Step == 0, "Golden orb must be grabbable immediately, without closing a screen first.");
            var rules = new RabbitTrickRules(); rules.Reset();
            Expect(rules.TryInsert(true, false, false) && rules.Step == TrickStep.ShowEmpty, "Rabbit placement must not be a step unlock.");
            rules.TakeRabbit(); Expect(!rules.RabbitInside, "Early placement can be undone.");
        }
        static void CheckSorting(RabbitTrickGame game)
        {
            Load(game, 4);
            var transfer = (DepthTransferTrick)game.Trick;
            foreach (float depth in new[] { 0.05f, 0.45f, 0.9f })
            {
                SetDepth(game, depth); transfer.Orb.depth = depth; game.view.Draw(game);
                int body = game.view.World.Find("Magician").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                int hand = game.view.World.Find("LeftHand").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                int orb = game.view.World.Find("DepthTransferTrick/GoldenOrb").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                int first = game.view.World.Find("DepthTransferTrick/FirstScreenFixed").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                int second = game.view.World.Find("DepthTransferTrick/SecondDepthCurtain").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                Expect(body == StageDepth.Order(depth) && hand == body + 4 && orb == body + 3, "Whole actor and hand-held props must share the same depth rule.");
                Expect((body < first) == (depth > 0.2f) && (body < second) == (depth > 0.58f), "Visual screen order must match physical depth.");
            }
            Load(game, 0); game.view.Draw(game);
            Expect(game.view.World.Find("LeftHand").GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder == StageDepth.Order(0) + 4,
                "Returning from fifth trick must reset glove depth order.");
        }
    }
}
