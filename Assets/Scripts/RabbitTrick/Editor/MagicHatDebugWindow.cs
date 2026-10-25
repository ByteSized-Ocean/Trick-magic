using UnityEditor;
using UnityEngine;

namespace Sleight.RabbitTrick.EditorTools
{
    public sealed class MagicHatDebugWindow : EditorWindow
    {
        [SerializeField] HatSpecies species = HatSpecies.Mouse;
        [SerializeField] bool golden;
        Vector2 scroll;

        [MenuItem("Sleight/Debug/Magic Hat _F7")]
        public static void Open()
        {
            var window = GetWindow<MagicHatDebugWindow>("Magic Hat Debug");
            window.minSize = new Vector2(400, 420); window.Show();
        }

        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            using (var area = new EditorGUILayout.ScrollViewScope(scroll))
            { scroll = area.scrollPosition; DrawControls(); }
        }

        void DrawControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("NEXT HAT ANIMAL", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Queue one animal, then click the hat on the main menu. After one successful reveal, normal random draws resume. The one-second cooldown still applies.", MessageType.Info);
            species = (HatSpecies)EditorGUILayout.EnumPopup("Animal", species);
            golden = EditorGUILayout.Toggle("Golden version", golden);
            EditorGUILayout.Space(6);
            var game = Object.FindAnyObjectByType<RabbitTrickGame>();
            var toy = game != null && game.Menu != null ? game.Menu.HatToy : null;
            bool running = EditorApplication.isPlaying && toy != null;
            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("QUEUE NEXT HAT CLICK", GUILayout.Height(32))) toy.QueueNextForEditor(species, golden);
                if (GUILayout.Button("QUEUE GOLDEN DIVINE DRAGON", GUILayout.Height(28)))
                { species = HatSpecies.DivineDragon; golden = true; toy.QueueNextForEditor(species, true); }
            }
            EditorGUILayout.Space(8);
            if (!running)
            { EditorGUILayout.HelpBox("Enter Play mode in the Game scene to use the hat debugger.", MessageType.Info); return; }
            bool queued = toy.HasQueuedForEditor;
            string next = queued ? (toy.QueuedForEditor.Golden ? "GOLDEN " : "") + toy.QueuedForEditor.Animal.Name : "RANDOM";
            EditorGUILayout.LabelField("Next successful click", next, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cooldown", toy.CooldownRemaining.ToString("0.0") + "s");
            using (new EditorGUI.DisabledScope(!queued))
                if (GUILayout.Button("CLEAR QUEUED ANIMAL")) toy.ClearQueuedForEditor();
            bool home = game.Menu.Page == StageMenu.ScreenPage.Home;
            if (!home)
            {
                EditorGUILayout.HelpBox("The queued animal stays armed while you are away from the main menu.", MessageType.Info);
                if (GUILayout.Button("OPEN MAIN MENU")) game.Menu.ShowHome();
            }
            using (new EditorGUI.DisabledScope(!home || !queued || toy.CooldownRemaining > 0))
                if (GUILayout.Button("REVEAL QUEUED ANIMAL NOW", GUILayout.Height(28))) toy.TryReveal();
            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!home || toy.ActiveVisitorCount == 0))
            {
                EditorGUI.BeginChangeCheck();
                float maximum = Mathf.Max(1, toy.LatestDurationForEditor - 0.3f);
                float seek = EditorGUILayout.Slider("Animation time", Mathf.Clamp(toy.LatestAgeForEditor, 0, maximum), 0, maximum);
                if (EditorGUI.EndChangeCheck()) toy.SeekLatestForEditor(seek);
            }
        }
    }
}
