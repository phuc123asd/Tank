using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Quản lý nhạc nền xuyên suốt các Scene (DontDestroyOnLoad).
    /// Giúp nhạc không bị ngắt quãng khi chuyển Scene nếu cùng một bài hát.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        private static MusicManager s_Instance;
        private AudioSource m_AudioSource;

        public static void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            if (s_Instance == null)
            {
                var go = new GameObject("MusicManager");
                s_Instance = go.AddComponent<MusicManager>();
                s_Instance.m_AudioSource = go.AddComponent<AudioSource>();
                s_Instance.m_AudioSource.loop = true;
                s_Instance.m_AudioSource.playOnAwake = false;
                DontDestroyOnLoad(go);
            }

            // Nếu nhạc đang phát giống bài hát yêu cầu thì giữ nguyên (không khởi động lại) để âm thanh liền mạch
            if (s_Instance.m_AudioSource.clip == clip && s_Instance.m_AudioSource.isPlaying)
            {
                return;
            }

            s_Instance.m_AudioSource.clip = clip;
            s_Instance.m_AudioSource.Play();
        }

        public static void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            if (s_Instance == null) return;

            // Dùng PlayOneShot để âm thanh có thể đè lên nhau (nhiều click cùng lúc)
            s_Instance.m_AudioSource.PlayOneShot(clip, 0.8f);
        }
    }
}
