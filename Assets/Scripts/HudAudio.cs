using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sleight
{
    // GDD §4.4 / §6 / §21：幕点、闪白、淡黑、音效。没有其它 UI。
    public class HudAudio : MonoBehaviour
    {
        public Image[] actDots = new Image[3];
        public Image flashWhite;
        public Image fadeBlack;
        public SpriteRenderer eye;   // 白灰眼睛剪影，不许用黄
        Coroutine eyeRoutine;

        public AudioSource sfxSource;
        public AudioSource heartbeatSource;
        public AudioClip sfxBell;
        public AudioClip sfxReveal;
        public AudioClip sfxBlocked;

        static readonly Color UiWhite = new Color32(0xD0, 0xD0, 0xD6, 0xFF);
        static readonly Color UiHollow = new Color32(0xD0, 0xD0, 0xD6, 0x40);   // TODO: 换成 1px 描边空心圆

        public void SetActDots(int completed)
        {
            for (int i = 0; i < actDots.Length; i++)
                if (actDots[i] != null) actDots[i].color = i < completed ? UiWhite : UiHollow;
        }

        public void FlashWhite()
        {
            if (flashWhite != null) StartCoroutine(Flash());
        }

        // 纯白 1 帧，再用 4 帧淡到 0。
        IEnumerator Flash()
        {
            SetAlpha(flashWhite, 1f);
            yield return null;
            for (int i = 3; i >= 0; i--)
            {
                SetAlpha(flashWhite, i / 4f);
                yield return null;
            }
        }

        public IEnumerator FadeBlack(float duration)
        {
            if (fadeBlack == null) yield break;
            float half = duration * 0.5f;
            for (float t = 0; t < half; t += Time.deltaTime) { SetAlpha(fadeBlack, t / half); yield return null; }
            SetAlpha(fadeBlack, 1f);
            for (float t = 0; t < half; t += Time.deltaTime) { SetAlpha(fadeBlack, 1f - t / half); yield return null; }
            SetAlpha(fadeBlack, 0f);
        }

        public void PlayBell() => PlayOneShot(sfxBell);
        public void PlayReveal() => PlayOneShot(sfxReveal);

        public void PlayBlocked()
        {
            PlayOneShot(sfxBlocked);
        }

        public void ShowEye(Zone zone, float duration)
        {
            if (eye == null) return;
            if (eyeRoutine != null) StopCoroutine(eyeRoutine);
            eye.transform.position = new Vector3(Stage.ZoneCenter(zone), 4.6f, 0f);
            eyeRoutine = StartCoroutine(EyeRoutine(duration));
        }

        public void SetEyeVisible(bool visible, Vector3 position)
        {
            if (eye == null) return;
            eye.transform.position = position;
            eye.gameObject.SetActive(visible);
        }

        IEnumerator EyeRoutine(float duration)
        {
            eye.gameObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            eye.gameObject.SetActive(false);
        }

        public IEnumerator FadeBlackTo(float alpha, float duration)
        {
            if (fadeBlack == null) yield break;
            float from = fadeBlack.color.a;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                SetAlpha(fadeBlack, Mathf.Lerp(from, alpha, t / duration));
                yield return null;
            }
            SetAlpha(fadeBlack, alpha);
        }

        // 怀疑度 > 60 时循环心跳，音量随怀疑度线性升。
        public void SetHeartbeat(float suspicion)
        {
            if (heartbeatSource == null || heartbeatSource.clip == null) return;
            bool on = suspicion > 60f;
            if (on && !heartbeatSource.isPlaying) heartbeatSource.Play();
            if (!on && heartbeatSource.isPlaying) heartbeatSource.Stop();
            heartbeatSource.volume = on ? Mathf.InverseLerp(60f, 100f, suspicion) : 0f;
        }

        public void MuteFor(float seconds) => StartCoroutine(Mute(seconds));

        IEnumerator Mute(float seconds)
        {
            AudioListener.volume = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            AudioListener.volume = 1f;
        }

        void PlayOneShot(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        static void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}
