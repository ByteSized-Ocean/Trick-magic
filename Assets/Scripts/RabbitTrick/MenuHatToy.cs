using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    public sealed class MenuHatToy : MonoBehaviour
    {
        sealed class Visitor
        {
            public RectTransform Rect;
            public HatAnimalGraphic Art;
            public float Born, Exit = -1;
            public bool Dragon;
            public HatDraw Draw;
            public int Route;
            public HatJourneyGraphic Environment;
            public Burst Aura;
            public bool DepartureSound, Thrown, Hit;
            public float NextFoley;
        }
        sealed class Burst { public HatRevealGraphic Art; public float Born; public Visitor Owner; }
        readonly List<Visitor> visitors = new List<Visitor>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly System.Random random = new System.Random();
        readonly AudioClip[] calls = new AudioClip[13];
        readonly Dictionary<string, AudioClip> journeySounds = new Dictionary<string, AudioClip>();
        RectTransform contents, animals, environment, fx, hat, arrow;
        Text prompt, nameLabel, chanceLabel;
        Button button;
        StageSoundtrack soundtrack;
        AudioSource voice, magic;
        AudioClip goldenSound, dragonSound;
        Font font;
        float nextClick, lastClick = -100;
#if UNITY_EDITOR
        [SerializeField, HideInInspector] bool editorHasNext;
        [SerializeField, HideInInspector] HatSpecies editorNextSpecies;
        [SerializeField, HideInInspector] bool editorNextGolden;
        public bool HasQueuedForEditor => editorHasNext;
        public HatDraw QueuedForEditor => new HatDraw(editorNextSpecies, editorNextGolden);

        public void QueueNextForEditor(HatSpecies species, bool golden = false)
        {
            if ((int)species < 0 || (int)species >= HatAnimalRoster.Animals.Length)
                throw new System.ArgumentOutOfRangeException(nameof(species));
            editorNextSpecies = species; editorNextGolden = golden; editorHasNext = true;
        }
        public void ClearQueuedForEditor() => editorHasNext = false;
        public float LatestAgeForEditor => visitors.Count > 0 ? Time.unscaledTime - visitors[visitors.Count - 1].Born : 0;
        public float LatestDurationForEditor => visitors.Count > 0 ? visitors[visitors.Count - 1].Environment.Duration : 0;
        public bool SeekLatestForEditor(float age)
        {
            if (visitors.Count == 0) return false;
            var item = visitors[visitors.Count - 1];
            float exitAge = item.Exit >= 0 ? Mathf.Min(item.Exit - item.Born, HatAnimalMotion.ExitAt(item.Draw.Species)) : HatAnimalMotion.ExitAt(item.Draw.Species);
            item.Born = Time.unscaledTime - Mathf.Max(0, age); item.Exit = item.Born + exitAge;
            if (item.Aura != null) item.Aura.Born = item.Born;
            RefreshVisuals(); return true;
        }
#endif
        public int RevealCount { get; private set; }
        public HatDraw LastDraw { get; private set; }
        public float CooldownRemaining => Mathf.Max(0, nextClick - Time.unscaledTime);
        public float VisitorAge => Time.unscaledTime - lastClick;
        public AudioSource VoiceSource => voice;
        public AudioSource MagicSource => magic;
        public AudioClip LastCall { get; private set; }
        public AudioClip LastDepartureCall { get; private set; }
        public int ActiveVisitorCount => visitors.Count;
        public bool HasDragonEffect { get { foreach (var v in visitors) if (v.Dragon) return true; foreach (var b in bursts) if (b.Art != null && b.Art.Draw.Species == HatSpecies.DivineDragon) return true; return false; } }

        public void Build(RectTransform parent, Font hudFont, StageSoundtrack audio)
        {
            Hide(); contents = parent; font = hudFont; soundtrack = audio;
            if (voice == null)
            {
                var stale = transform.Find("HatSounds");
                if (stale != null) { foreach (var old in stale.GetComponents<AudioSource>()) old.Stop(); Destroy(stale.gameObject); }
                var source = new GameObject("HatSounds"); source.transform.SetParent(transform, false);
                voice = source.AddComponent<AudioSource>(); magic = source.AddComponent<AudioSource>();
                voice.playOnAwake = magic.playOnAwake = false; voice.spatialBlend = magic.spatialBlend = 0;
            }
            // Audio sources survive an editor reload; the non-serialized clip cache does not.
            for (int i = 0; i < calls.Length; i++) calls[i] = Resources.Load<AudioClip>("HatAudio/" + HatAnimalRoster.Animals[i].Sound);
            goldenSound = Resources.Load<AudioClip>("HatAudio/GoldenChime"); dragonSound = Resources.Load<AudioClip>("HatAudio/DragonFanfare");
            foreach (string cue in new[] { "WindGust", "PawRun", "Stampede", "BananaThrow", "BananaSplat", "IceSlide", "OceanSplash", "SwimBubbles", "CloudFlight" })
                journeySounds[cue] = Resources.Load<AudioClip>("HatAudio/" + cue);
            var hit = Box("HatButton", contents, 281, 614, 235, 132, Color.clear); hit.raycastTarget = true;
            button = hit.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
            button.onClick.AddListener(() => TryReveal());
            hat = Group("HatArtwork", hit.transform, 0, 0, 235, 132);
            Box("HatShadow", hat, 6, 116, 225, 11, new Color(0, 0, 0, 0.25f));
            Box("HatCrown", hat, 41, 11, 150, 93, new Color32(82, 49, 97, 255));
            Box("HatHighlight", hat, 45, 15, 8, 65, new Color32(118, 78, 122, 255));
            Box("HatBand", hat, 41, 77, 150, 16, new Color32(115, 43, 68, 255));
            Box("HatBrim", hat, 8, 105, 215, 7, new Color32(210, 156, 79, 255));
            Box("HatOpening", hat, 41, 9, 150, 5, new Color32(24, 20, 33, 255));
            prompt = Label("HatPrompt", contents, 262, 555, 270, 27, "CLICK THE HAT", 18, new Color32(210, 156, 79, 255));
            prompt.alignment = TextAnchor.MiddleCenter;
            arrow = Group("HatHintArrow", contents, 385, 588, 24, 29);
            var arrowColor = new Color32(242, 222, 188, 255);
            Box("Stem", arrow, 10, 0, 3, 25, arrowColor);
            var left = Box("ArrowLeft", arrow, 1, 16, 15, 3, arrowColor); left.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var right = Box("ArrowRight", arrow, 11, 27, 15, 3, arrowColor); right.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            nameLabel = Label("HatAnimalName", contents, 510, 746, 315, 36, "A LITTLE MAGIC?", 23, new Color32(242, 222, 188, 255));
            nameLabel.resizeTextForBestFit = true; nameLabel.resizeTextMinSize = 14; nameLabel.resizeTextMaxSize = 23;
            chanceLabel = Label("HatAnimalChance", contents, 510, 782, 315, 26, "ONE SURPRISE EVERY SECOND", 14, new Color(0.72f, 0.65f, 0.70f));
            environment = Group("HatEnvironment", contents, 0, 0, 1600, 900);
            animals = Group("HatVisitors", contents, 0, 0, 1600, 900);
            fx = Group("HatMagic", contents, 0, 0, 1600, 900);
        }
        public void Hide()
        {
            foreach (var item in visitors)
            {
                if (item.Rect != null) { item.Rect.gameObject.SetActive(false); Destroy(item.Rect.gameObject); }
                if (item.Environment != null) { item.Environment.gameObject.SetActive(false); Destroy(item.Environment.gameObject); }
            }
            foreach (var item in bursts) if (item.Art != null) { item.Art.gameObject.SetActive(false); Destroy(item.Art.gameObject); }
            visitors.Clear(); bursts.Clear();
            if (voice != null) voice.Stop(); if (magic != null) magic.Stop();
            contents = null;
        }
        public bool TryReveal()
        {
            // Rejected clicks consume no random tickets and produce no voice or animation.
            if (contents == null || !contents.gameObject.activeInHierarchy || CooldownRemaining > 0) return false;
#if UNITY_EDITOR
            if (editorHasNext)
            {
                bool revealed = Emit(QueuedForEditor);
                if (revealed) editorHasNext = false;
                return revealed;
            }
#endif
            return Emit(HatAnimalRoster.Resolve(random.Next(HatAnimalRoster.Tickets), random.Next(100)));
        }
#if UNITY_EDITOR
        public bool PreviewForEditor(HatSpecies species, bool golden)
        { if (contents == null || CooldownRemaining > 0) return false; return Emit(new HatDraw(species, golden)); }
#endif
        bool Emit(HatDraw draw)
        {
            float now = Time.unscaledTime; nextClick = now + HatAnimalRoster.ClickInterval; lastClick = now;
            LastDraw = draw; RevealCount++;
            foreach (var old in visitors) if (old.Exit < 0) old.Exit = Mathf.Max(now, old.Born + 1.4f);
            bool dragon = draw.Species == HatSpecies.DivineDragon;
            var rt = Group("Visitor_" + draw.Animal.Name, animals, 397, 627, dragon ? 760 : 300, dragon ? 450 : 225);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var art = rt.gameObject.AddComponent<HatAnimalGraphic>(); art.raycastTarget = false; art.Species = draw.Species; art.Golden = draw.Golden;
            var envRoot = Group("Journey_" + draw.Animal.Name, environment, 0, 0, 1600, 900);
            var journey = envRoot.gameObject.AddComponent<HatJourneyGraphic>(); journey.raycastTarget = false;
            journey.Draw = draw; journey.Route = (RevealCount - 1) % 4; journey.ExitAge = HatAnimalMotion.ExitAt(draw.Species);
            var visitor = new Visitor { Rect = rt, Art = art, Born = now, Dragon = dragon, Draw = draw, Route = journey.Route, Environment = journey };
            visitors.Add(visitor);
            var effectRoot = Group("Aura_" + draw.Animal.Name, fx, 0, 0, 1600, 900);
            var effect = effectRoot.gameObject.AddComponent<HatRevealGraphic>(); effect.raycastTarget = false; effect.Draw = draw; effect.Seed = random.Next(100000);
            visitor.Aura = new Burst { Art = effect, Born = now, Owner = visitor }; bursts.Add(visitor.Aura);
            // Dragon art gets a full-stage entrance above its prismatic field.
            if (dragon) rt.SetParent(fx, false);
            nameLabel.text = (draw.Golden ? "GOLDEN " : "") + draw.Animal.Name;
            nameLabel.color = draw.Golden ? new Color(1, 0.85f, 0.35f) : draw.Animal.Tier == 0 ? new Color32(242, 222, 188, 255) : draw.Animal.Aura;
            chanceLabel.text = (draw.Golden ? "GOLDEN CHANCE " : "SPECIES CHANCE ") + draw.Percent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";
            SetVolume();
            var call = calls[(int)draw.Species]; LastCall = call; if (call != null) voice.PlayOneShot(call, 0.8f);
            if (draw.Species == HatSpecies.Orca) PlayJourney("OceanSplash", 0.55f);
            if (draw.Species == HatSpecies.EmperorPenguin) PlayJourney("IceSlide", 0.4f);
            if (dragon && dragonSound != null) magic.PlayOneShot(dragonSound, 0.65f);
            if (draw.Golden && goldenSound != null) magic.PlayOneShot(goldenSound, 0.85f);
            return true;
        }
        void SetVolume()
        { float volume = soundtrack != null ? soundtrack.EffectsVolume : 0.7f; if (voice != null) voice.volume = volume * 0.75f; if (magic != null) magic.volume = volume * 0.7f; }
        void PlayJourney(string name, float volume = 0.5f)
        { if (magic != null && journeySounds.TryGetValue(name, out var clip) && clip != null) magic.PlayOneShot(clip, volume); }
        void DepartureAudio(Visitor item, HatAnimalPose pose)
        {
            if (!pose.Departing || pose.Finished) return;
            var species = item.Draw.Species;
            if (!item.DepartureSound)
            {
                item.DepartureSound = true; LastDepartureCall = calls[(int)species];
                if (LastDepartureCall != null) voice.PlayOneShot(LastDepartureCall, 0.85f);
                if (item.Draw.Golden && goldenSound != null) magic.PlayOneShot(goldenSound, 0.3f);
                string cue = species == HatSpecies.Eagle ? "WindGust" : species == HatSpecies.Elephant ? "Stampede"
                    : species == HatSpecies.EmperorPenguin ? "IceSlide" : species == HatSpecies.Orca ? "SwimBubbles"
                    : species == HatSpecies.DivineDragon ? "CloudFlight" : species == HatSpecies.Bear || species == HatSpecies.Lion || species == HatSpecies.Tiger ? "PawRun" : null;
                if (cue != null) PlayJourney(cue, 0.55f);
                item.NextFoley = 2.4f;
            }
            if (species == HatSpecies.Monkey)
            {
                if (!item.Thrown && pose.DepartureAge >= 0.6f) { item.Thrown = true; PlayJourney("BananaThrow", 0.6f); }
                if (!item.Hit && pose.DepartureAge >= 2.8f) { item.Hit = true; PlayJourney("BananaSplat", 0.5f); }
            }
            if (pose.DepartureAge >= item.NextFoley)
            {
                string cue = species == HatSpecies.Elephant ? "Stampede" : species == HatSpecies.EmperorPenguin ? "IceSlide"
                    : species == HatSpecies.Orca ? "SwimBubbles" : species == HatSpecies.Bear || species == HatSpecies.Lion || species == HatSpecies.Tiger ? "PawRun" : null;
                if (cue != null) PlayJourney(cue, 0.28f);
                item.NextFoley += 2.4f;
            }
        }
        void Update() => RefreshVisuals();
        public void RefreshVisuals()
        {
            if (contents == null || !contents.gameObject.activeInHierarchy) return;
            float now = Time.unscaledTime; SetVolume();
            button.interactable = CooldownRemaining <= 0;
            prompt.text = button.interactable ? "CLICK THE HAT" : "A LITTLE PATIENCE...";
            arrow.anchoredPosition = new Vector2(385, -588 + Mathf.Sin(now * 4) * 4);
            float bump = Mathf.Clamp01(1 - (now - lastClick) / 0.4f);
            hat.localScale = new Vector3(1 + Mathf.Sin(bump * Mathf.PI) * 0.07f, 1 - Mathf.Sin(bump * Mathf.PI) * 0.09f, 1);
            hat.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bump * Mathf.PI * 2) * bump * 5);
            for (int i = visitors.Count - 1; i >= 0; i--)
            {
                var item = visitors[i]; float age = now - item.Born;
                float exitAge = item.Exit >= 0 ? Mathf.Min(item.Exit - item.Born, HatAnimalMotion.ExitAt(item.Draw.Species)) : HatAnimalMotion.ExitAt(item.Draw.Species);
                var pose = HatAnimalMotion.Sample(item.Draw.Species, age, item.Route, exitAge);
                item.Environment.Animate(age, exitAge);
                if (age > item.Environment.Duration)
                { Destroy(item.Rect.gameObject); Destroy(item.Environment.gameObject); visitors.RemoveAt(i); continue; }
                DepartureAudio(item, pose);
                item.Rect.gameObject.SetActive(!pose.Finished);
                if (!pose.Finished)
                {
                    item.Rect.anchoredPosition = new Vector2(pose.Position.x, -pose.Position.y);
                    item.Rect.localScale = new Vector3(pose.Scale.x, pose.Scale.y, 1);
                    item.Rect.localRotation = Quaternion.Euler(0, 0, pose.Rotation);
                    item.Art.color = new Color(1, 1, 1, pose.Alpha); item.Art.SetDeparture(pose.DepartureAge, pose.Departing); item.Art.Animate(age);
                }
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var item = bursts[i]; float age = now - item.Born;
                if (age > item.Art.Duration)
                {
                    if (item.Owner != null && visitors.Contains(item.Owner)) item.Art.gameObject.SetActive(false);
                    else { Destroy(item.Art.gameObject); bursts.RemoveAt(i); }
                }
                else { item.Art.gameObject.SetActive(true); item.Art.Animate(age); }
            }
        }
        static RectTransform Group(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        static Image Box(string name, Transform parent, float x, float y, float w, float h, Color c)
        { var rt = Group(name, parent, x, y, w, h); var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im; }
        Text Label(string name, Transform parent, float x, float y, float w, float h, string value, int size, Color c)
        { var rt = Group(name, parent, x, y, w, h); var text = rt.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size; text.color = c; text.text = value; text.raycastTarget = false; return text; }
    }
}
