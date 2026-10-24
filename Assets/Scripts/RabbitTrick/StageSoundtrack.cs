using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class StageSoundtrack : MonoBehaviour
    {
        static readonly string[] Names = { "Level1_Bach_Badinerie", "Level2_Mozart_Allegro", "Level3_Bach_Aria", "Level4_Vivaldi_Mandolin", "Level5_Bach_Preludio" };
        public AudioClip[] Tracks { get; private set; }
        public AudioClip Applause { get; private set; }
        public AudioSource Music { get; private set; }
        AudioSource cheers;
        float cheerAge = -1;
        public float MusicVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public float MasterVolume { get; private set; }
        bool softened;
        public void Initialize()
        {
            Music = gameObject.AddComponent<AudioSource>(); Music.playOnAwake = false; Music.loop = true; Music.spatialBlend = 0;
            cheers = gameObject.AddComponent<AudioSource>(); cheers.playOnAwake = false; cheers.spatialBlend = 0;
            Tracks = new AudioClip[Names.Length];
            for (int i = 0; i < Names.Length; i++) Tracks[i] = Resources.Load<AudioClip>("StageAudio/" + Names[i]);
            Applause = Resources.Load<AudioClip>("StageAudio/TheatreApplause");
            MasterVolume = PlayerPrefs.GetFloat("Sleight.Master", 0.8f);
            MusicVolume = PlayerPrefs.GetFloat("Sleight.Music", 0.32f);
            EffectsVolume = PlayerPrefs.GetFloat("Sleight.Effects", 0.7f);
            Apply();
        }
        public void PlayLevel(int level)
        {
            if (Music == null || Tracks == null) return;
            var next = Tracks[Mathf.Clamp(level, 0, Tracks.Length - 1)];
            if (Music.clip == next && Music.isPlaying) return;
            Music.clip = next; if (next != null) Music.Play();
        }
        public void SetMenu(bool on) { softened = on; Apply(); }
        public void SetMaster(float value) { MasterVolume = value; PlayerPrefs.SetFloat("Sleight.Master", value); Apply(); }
        public void SetMusic(float value) { MusicVolume = value; PlayerPrefs.SetFloat("Sleight.Music", value); Apply(); }
        public void SetEffects(float value) { EffectsVolume = value; PlayerPrefs.SetFloat("Sleight.Effects", value); Apply(); }
        void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (Music != null) Music.volume = MusicVolume * (softened ? 0.7f : 1f);
            var game = GetComponent<RabbitTrickGame>();
            if (game != null && game.audioSource != null) game.audioSource.volume = EffectsVolume;
        }
        public void PlayApplause()
        {
            if (cheers == null || Applause == null) return;
            cheers.clip = Applause; cheers.time = Mathf.Min(2f, Applause.length * 0.1f);
            cheers.volume = 0; cheers.Play(); cheerAge = 0;
        }
        void Update()
        {
            if (cheerAge < 0) return;
            cheerAge += Time.unscaledDeltaTime;
            cheers.volume = EffectsVolume * Mathf.Min(Mathf.Clamp01(cheerAge / 0.25f), Mathf.Clamp01((6.5f - cheerAge) / 1.5f));
            if (cheerAge >= 6.5f) { cheers.Stop(); cheerAge = -1; }
        }
        public void StopEffects() { if (cheers != null) cheers.Stop(); cheerAge = -1; }
        void OnApplicationQuit() => PlayerPrefs.Save();
    }
}
