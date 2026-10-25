using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    [DefaultExecutionOrder(-200)]
    public sealed class StageMenu : MonoBehaviour
    {
        public enum ScreenPage { None, Home, Levels, Settings, Pause }
        public ScreenPage Page { get; private set; }
        public bool BlocksGameplay => Page != ScreenPage.None;
        public Canvas Canvas { get; private set; }
        RabbitTrickGame game;
        RectTransform contents;
        Font font;
        ScreenPage settingsReturn;
        int programmePage;
        static readonly Color Ink = new Color32(25, 20, 35, 255), Velvet = new Color32(92, 31, 52, 255);
        static readonly Color Gold = new Color32(210, 156, 79, 255), Cream = new Color32(242, 222, 188, 255), Teal = new Color32(100, 220, 199, 255);
        public void Initialize(RabbitTrickGame owner)
        {
            game = owner; font = game.view.HudFont;
            var stale = transform.Find("TheatreMenus");
            if (stale != null) { stale.gameObject.SetActive(false); Destroy(stale.gameObject); }
            var go = new GameObject("TheatreMenus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            Canvas = go.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 30000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            var root = go.GetComponent<RectTransform>();
            var background = Box("Midnight", root, 0, 0, 1600, 900, Ink); background.raycastTarget = true;
            for (int i = 0; i < 20; i++) Box("Wallpaper", root, 120 + i * 72, 120, 2, 660, new Color(0.32f, 0.25f, 0.37f, 0.26f));
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < 9; i++) Box("VelvetFold", root, side == 0 ? i * 15 : 1600 - (i + 1) * 15, 0, 18, 900,
                    i % 3 == 0 ? new Color32(117, 42, 67, 255) : i % 3 == 1 ? Velvet : new Color32(64, 24, 45, 255));
            Box("Valance", root, 0, 0, 1600, 74, Velvet);
            Box("GoldTrim", root, 0, 72, 1600, 3, Gold);
            Box("BottomTrim", root, 0, 827, 1600, 2, Gold * new Color(1, 1, 1, 0.45f));
            Text("Masthead", root, 175, 104, 1160, 24, "THE GRAND ILLUSION     /     THE STAGE AWAITS", 17, Gold);
            for (int i = 0; i < 12; i++)
            {
                var dot = Box("Star", root, 180 + i * 109, 170 + (i * 73 % 550), 5, 5, new Color(0.83f, 0.65f, 0.37f, 0.35f));
                dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            Text("Footer", root, 175, 849, 1250, 24, "Perform with both hands. Let the audience see only the miracle.", 18, new Color(0.72f, 0.65f, 0.70f));
            contents = Box("Contents", root, 0, 0, 1600, 900, Color.clear).rectTransform;
            if (EventSystem.current == null)
            {
                var events = new GameObject("TheatreEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        void Update()
        {
            if (game == null || Keyboard.current == null) return;
            var kb = Keyboard.current;
            if (kb.pKey.wasPressedThisFrame)
            {
                if (Page == ScreenPage.None) ShowPause();
                else if (Page == ScreenPage.Pause) Resume();
                else if (Page == ScreenPage.Settings && settingsReturn == ScreenPage.Pause) ShowPause();
                return;
            }
            if (Page == ScreenPage.Pause && kb.rKey.wasPressedThisFrame) Restart();
            if (kb.escapeKey.wasPressedThisFrame && Page == ScreenPage.Settings) BackFromSettings();
        }
        void OnEnable()
        {
            // Editor assembly reloads discard runtime UnityEvent listeners. Rebuild the visible menu.
            if (Canvas == null) return;
            var owner = GetComponent<RabbitTrickGame>();
            var restored = Page; var back = settingsReturn;
            Initialize(owner);
            if (restored == ScreenPage.Home) ShowHome();
            else if (restored == ScreenPage.Levels) ShowLevels();
            else if (restored == ScreenPage.Settings) ShowSettings(back);
            else if (restored == ScreenPage.Pause) ShowPause();
            else Hide();
        }
        void Clear(ScreenPage page)
        {
            Page = page;
            foreach (Transform child in contents) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Canvas.gameObject.SetActive(true);
            if (game.view.HudCanvas != null) game.view.HudCanvas.gameObject.SetActive(false);
            Time.timeScale = 0;
            game.Soundtrack?.SetMenu(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Hide()
        {
            Page = ScreenPage.None; Time.timeScale = 1;
            if (Canvas != null) Canvas.gameObject.SetActive(false);
            if (game.view.HudCanvas != null) game.view.HudCanvas.gameObject.SetActive(true);
            game.Soundtrack?.SetMenu(false);
        }
        void Heading(string eyebrow, string title, string description)
        {
            Text("Section", contents, 190, 190, 1150, 30, eyebrow, 18, Gold);
            Text("Title", contents, 188, 230, 1200, 90, title, 52, Cream);
            Text("Description", contents, 192, 325, 1150, 60, description, 21, new Color(0.72f, 0.65f, 0.70f));
        }
        public void ShowHome()
        {
            game.CancelForHome();
            Clear(ScreenPage.Home); game.Soundtrack?.PlayLevel(2);
            Text("Kicker", contents, 200, 236, 600, 30, "A LITTLE MISDIRECTION", 20, Gold);
            Text("GameName", contents, 195, 281, 650, 120, "SLEIGHT", 86, Cream);
            Box("TitleRule", contents, 200, 425, 430, 3, Gold);
            Text("Tagline", contents, 200, 456, 580, 70, "In the light, a miracle.\nOut of sight, a secret.", 26, new Color(0.75f, 0.67f, 0.73f));
            // A small geometric top hat echoes the props on stage.
            Box("HatCrown", contents, 322, 592, 150, 93, new Color32(82, 49, 97, 255));
            Box("HatBand", contents, 322, 658, 150, 16, Velvet);
            Box("HatBrim", contents, 289, 686, 215, 7, Gold);
            Box("HomePanel", contents, 888, 225, 505, 516, new Color32(37, 28, 45, 255));
            Box("PanelRule", contents, 920, 251, 440, 2, Gold);
            Text("Programme", contents, 920, 275, 440, 30, "TONIGHT'S PROGRAMME", 19, Gold);
            Button("StartGame", "START GAME", 920, 338, 440, () => game.BeginLevel(0));
            Button("ViewLevels", "VIEW LEVELS", 920, 424, 440, ShowLevels);
            Button("Settings", "SETTINGS", 920, 510, 440, () => ShowSettings(ScreenPage.Home));
            Button("QuitGame", "QUIT GAME", 920, 596, 440, Quit);
        }
        public void ShowLevels()
        {
            Clear(ScreenPage.Levels);
            Heading(programmePage == 0 ? "THE PROGRAMME / ACT I: SLEIGHT OF HAND" : "THE PROGRAMME / ACT II: GRAND ILLUSIONS", "CHOOSE YOUR TRICK", "All ten acts are available. Choose a trick or turn the programme page.");
            string[] names = { "THE RABBIT", "THE SUBSTITUTION", "THE FLOATING LADY", "THE FLOWERING HAT", "THE GOLDEN TRANSFER",
                "THE INVISIBLE CARD", "THE WATER VAULT", "THE MIRROR SPLIT", "THE STILL RAIN", "THE VANISHING VAULT" };
            string[] detail = { "Hide a rabbit while the light is away.", "Trade the assistant for a dummy.", "Hide the support. Prove she can float.", "Release the dove. Reveal the flowers.", "Carry the orb behind the depth screens.",
                "Two palms conceal one impossible card.", "Drain the tank. Beat the air clock.", "Tune the mirrors before using the blade.", "Freeze the water. Make it rise.", "Balance the vault. Make it vanish." };
            for (int item = 0; item < 5; item++)
            {
                int i = programmePage * 5 + item;
                int choice = i;
                float x = 190 + (item % 3) * 407, y = 405 + (item / 3) * 155;
                var card = Button("Level" + (i + 1), "", x, y, 382, () => game.BeginLevel(choice), 133);
                Text("Number", card.transform, 20, 14, 60, 25, (i + 1).ToString("00"), 18, Gold);
                Text("Name", card.transform, 20, 43, 340, 40, names[i], 28, Cream);
                Text("Detail", card.transform, 20, 89, 342, 30, detail[i], 17, new Color(0.71f, 0.64f, 0.71f));
            }
            Button(programmePage == 0 ? "NextActs" : "PreviousActs", programmePage == 0 ? "ACT II: TRICKS 06 - 10  >" : "<  ACT I: TRICKS 01 - 05", 190, 712, 620, () => { programmePage = 1 - programmePage; ShowLevels(); }, 57);
            Button("BackHome", "MAIN MENU", 1030, 712, 378, ShowHome, 57);
        }
        public void ShowPause()
        {
            Clear(ScreenPage.Pause);
            Heading("INTERMISSION", "SHOW PAUSED", "The stage is paused. Press P to resume; R restarts only while this menu is open.");
            Button("ResumeGame", "RESUME    P", 190, 413, 550, Resume);
            Button("RestartLevel", "RESTART LEVEL    R", 190, 500, 550, Restart);
            Button("PlayDemo", "PLAY THIS DEMO", 190, 587, 550, PlayDemo);
            Button("PauseSettings", "SETTINGS", 844, 413, 550, () => ShowSettings(ScreenPage.Pause));
            Button("ReturnHome", "MAIN MENU", 844, 500, 550, ShowHome);
            Button("PauseQuit", "QUIT GAME", 844, 587, 550, Quit);
        }
        public void ShowSettings(ScreenPage back)
        {
            settingsReturn = back; Clear(ScreenPage.Settings);
            Heading("SETTINGS / SOUND & DISPLAY", "SET THE STAGE", "Changes apply immediately and save automatically. Effects include applause and stage cues.");
            VolumeSlider("Master", "MASTER VOLUME", 418, game.Soundtrack.MasterVolume, game.Soundtrack.SetMaster);
            VolumeSlider("Music", "CLASSICAL MUSIC", 500, game.Soundtrack.MusicVolume, game.Soundtrack.SetMusic);
            VolumeSlider("Effects", "SOUND EFFECTS", 582, game.Soundtrack.EffectsVolume, game.Soundtrack.SetEffects);
            Button fullscreen = null;
            fullscreen = Button("Fullscreen", Screen.fullScreen ? "SWITCH TO WINDOWED" : "SWITCH TO FULLSCREEN", 190, 705, 490, () =>
            {
                bool next = !Screen.fullScreen; Screen.fullScreen = next;
                fullscreen.GetComponentInChildren<Text>().text = next ? "SWITCH TO WINDOWED" : "SWITCH TO FULLSCREEN";
            });
            Button("BackSettings", "BACK", 955, 705, 440, BackFromSettings);
        }
        void BackFromSettings() { PlayerPrefs.Save(); if (settingsReturn == ScreenPage.Pause) ShowPause(); else ShowHome(); }
        public void Resume() => Hide();
        public void Restart() { if (Page != ScreenPage.Pause) return; Hide(); game.RestartLevel(); }
        public void PlayDemo() { Hide(); game.ReplayDemo(); }
        public void Quit()
        {
            PlayerPrefs.Save(); Time.timeScale = 1;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void VolumeSlider(string id, string title, float y, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            Text(id + "Title", contents, 192, y + 6, 250, 40, title, 25, Cream);
            var number = Text(id + "Value", contents, 1290, y + 5, 100, 40, Mathf.RoundToInt(value * 100f) + "%", 22, Gold);
            var sliderGo = new GameObject(id + "Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(contents, false); Position(sliderGo.GetComponent<RectTransform>(), 495, y, 730, 48);
            var slider = sliderGo.GetComponent<Slider>();
            var hit = Box("HitArea", sliderGo.transform, 0, 0, 730, 48, Color.clear); hit.raycastTarget = true;
            var rail = Box("Rail", sliderGo.transform, 0, 20, 730, 6, new Color32(70, 52, 73, 255)); rail.raycastTarget = true;
            var fillArea = Box("FillArea", sliderGo.transform, 0, 21, 730, 6, Color.clear);
            var handleArea = Box("HandleArea", sliderGo.transform, 0, 24, 730, 0, Color.clear);
            var fill = Box("Fill", fillArea.transform, 0, 0, 730, 6, Gold);
            var handle = Box("Handle", handleArea.transform, 0, 0, 22, 24, Cream); handle.raycastTarget = true;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.anchoredPosition = Vector2.zero; handle.rectTransform.sizeDelta = new Vector2(22, 24);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            slider.onValueChanged.AddListener(v => { changed(v); number.text = Mathf.RoundToInt(v * 100f) + "%"; });
        }
        public Button FindButton(string id) => contents.Find(id)?.GetComponent<Button>();
        Button Button(string id, string caption, float x, float y, float width, UnityEngine.Events.UnityAction action, float height = 68)
        {
            var bg = Box(id, contents, x, y, width, height, new Color32(64, 39, 57, 255)); bg.raycastTarget = true;
            Box("Edge", bg.transform, 0, 0, 4, height, Gold);
            var button = bg.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.45f, 1.33f, 1.15f); colors.pressedColor = new Color(0.7f, 0.85f, 0.8f); colors.selectedColor = colors.highlightedColor;
            button.colors = colors; button.onClick.AddListener(action);
            var text = Text("Label", bg.transform, 25, 0, width - 42, height, caption, 25, Cream); text.alignment = TextAnchor.MiddleLeft;
            return button;
        }
        static void Position(RectTransform rt, float x, float y, float w, float h)
        { rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }
        Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, w, h);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        Text Text(string name, Transform parent, float x, float y, float w, float h, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = color; label.text = value; label.raycastTarget = false;
            return label;
        }
        void OnDestroy() { Time.timeScale = 1; }
    }
}
