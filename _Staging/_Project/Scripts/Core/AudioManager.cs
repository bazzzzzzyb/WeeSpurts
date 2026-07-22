using UnityEngine;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Minimal audio stub. Real mixing/music comes much later (Roadmap [1]
    /// only asks for a stub). Anything that wants to make noise calls
    /// AudioManager.Instance.PlaySfx(clip) and never touches AudioSources.
    ///
    /// SETUP: lives on the same GameObject as GameManager. An AudioSource is
    /// added automatically.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _source;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        /// <summary>Fire-and-forget sound effect. Volume 0..1.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return; // silent until real audio exists — never a crash
            _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
