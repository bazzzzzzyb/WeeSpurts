using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Grown from the old 35-line stub into a small pooled SFX player plus one
    /// looping ambience slot — not more than that, per the exec-day plan's own
    /// "this is a party game, not an audio-driven one." Anything that wants to
    /// make noise calls a sound ID (see <see cref="SoundId"/>) on
    /// AudioManager.Instance and never touches an AudioSource itself.
    ///
    /// SETUP: lives on the same GameObject as GameManager (GreyboxSceneBuilder
    /// and ThunderLanesVenueStationSetupTool both add it there and wire an
    /// AudioCatalog asset onto it). An AudioSource is added automatically for
    /// the ambience slot; the one-shot pool is built at runtime as child
    /// GameObjects, one per voice.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("Sound id -> clip(s). Wired by the venue setup tools; assign by hand for a scene that skips them. Missing ids are silent, never an error.")]
        [SerializeField] private AudioCatalog catalog;

        [Tooltip("How many one-shot sounds can overlap at once. A pin rack breaking hits several ids in one frame, so this wants headroom.")]
        [SerializeField] private int poolSize = 12;

        private AudioSource _ambienceSource; // the RequireComponent-added source, reserved for the one looping ambience slot
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private int _nextVoice;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _ambienceSource = GetComponent<AudioSource>();
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f; // ambience is everywhere, not positional

            for (int i = 0; i < poolSize; i++)
            {
                var voice = new GameObject($"SfxVoice_{i}");
                voice.transform.SetParent(transform, worldPositionStays: false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                _pool.Add(source);
            }

            // Auto-starts if a clip is assigned, silent no-op if not — every
            // scene shares one AudioCatalog asset (same convention as
            // EconomyConfig/VendorConfig), so dropping an alley-murmur clip
            // into the ambience_alley row makes every scene using it ambient
            // without any further wiring.
            SetAmbience(SoundId.AmbienceAlley);
        }

        /// <summary>Non-positional fire-and-forget cue — UI clicks, ticket dispense, the slot jackpot (deliberately heard venue-wide).</summary>
        public void PlaySfx(string id, float volume = 1f) => PlayInternal(id, position: null, volume, seed: null);

        /// <summary>Positional fire-and-forget cue at a world point — pin crashes, ball/lane impacts.</summary>
        public void PlaySfxAt(string id, Vector3 position, float volume = 1f) => PlayInternal(id, position, volume, seed: null);

        /// <summary>
        /// Positional cue whose variation is picked from <paramref name="seed"/>
        /// instead of live randomness. Reserve this for sounds a future
        /// networked replay would need every client to agree on (the ball's
        /// own throw sounds, keyed off LaunchParameters.Seed) — everything
        /// else uses plain UnityEngine.Random, the same split Block 1's
        /// SlotMachine/DrinkMeter work already drew for non-throw-driven
        /// randomness: "a pin crash that sounds different on two machines is
        /// fine."
        /// </summary>
        public void PlaySfxAtSeeded(string id, Vector3 position, int seed, float volume = 1f) =>
            PlayInternal(id, position, volume, seed);

        /// <summary>
        /// A single clip for a caller that owns its own looping AudioSource
        /// (BowlingBall's roll loop) rather than firing a pooled one-shot.
        /// Null if the id is unknown or has no clips assigned yet.
        /// </summary>
        public AudioClip GetClip(string id)
        {
            if (catalog == null || !catalog.TryGetEntry(id, out AudioCatalog.SoundEntry entry)) return null;
            if (entry.Clips == null || entry.Clips.Length == 0) return null;
            return entry.Clips[Random.Range(0, entry.Clips.Length)];
        }

        /// <summary>Loops a single ambience clip venue-wide. An empty/unknown id (or no clip assigned yet) stops whatever is playing.</summary>
        public void SetAmbience(string id, float volume = 1f)
        {
            AudioClip clip = GetClip(id);
            if (clip == null)
            {
                _ambienceSource.Stop();
                _ambienceSource.clip = null;
                return;
            }
            if (_ambienceSource.clip == clip && _ambienceSource.isPlaying) return; // already playing this one — don't restart it from the top
            _ambienceSource.clip = clip;
            _ambienceSource.volume = Mathf.Clamp01(volume);
            _ambienceSource.Play();
        }

        private void PlayInternal(string id, Vector3? position, float volume, int? seed)
        {
            if (catalog == null || !catalog.TryGetEntry(id, out AudioCatalog.SoundEntry entry)) return; // silent, exactly like the old stub's "clip == null? return"
            if (entry.Clips == null || entry.Clips.Length == 0) return;

            int index = seed.HasValue
                ? new System.Random(seed.Value).Next(entry.Clips.Length)
                : Random.Range(0, entry.Clips.Length);
            AudioClip clip = entry.Clips[index];
            if (clip == null) return;

            AudioSource voice = NextVoice();
            voice.transform.position = position ?? transform.position;
            voice.spatialBlend = position.HasValue ? 1f : 0f;
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * entry.Volume));
        }

        private AudioSource NextVoice()
        {
            // Round-robin, not "find an idle one": a burst of six pin crashes
            // in the same frame is exactly the case this pool exists for, and
            // cutting the OLDEST one off to make room for the newest is the
            // right trade in a party game — nobody notices a truncated tail,
            // everybody notices a silent 7th pin.
            AudioSource voice = _pool[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _pool.Count;
            return voice;
        }
    }
}
