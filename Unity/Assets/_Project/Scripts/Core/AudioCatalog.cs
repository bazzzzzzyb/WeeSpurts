using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Sound id -> clip(s), data only. The array (not a single clip) is the
    /// whole point: a pin crash with three takes doesn't sound like a loop
    /// pedal. Config, not code, per CodingStandards.md — Tony drops clips into
    /// the Inspector, nobody edits a switch statement to add a sound.
    ///
    /// SETUP: one asset, auto-created at
    /// Assets/_Project/ScriptableObjects/AudioCatalog.asset by the venue setup
    /// tools (same LoadOrCreateAsset pattern as EconomyConfig/VendorConfig),
    /// pre-seeded with every id the game currently calls by name so dropping a
    /// clip in is the only step left. Missing/empty clips on any row are
    /// silent, never an error — the game must work with zero audio content.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "WeeSpurts/Audio Catalog")]
    public class AudioCatalog : ScriptableObject
    {
        [System.Serializable]
        public class SoundEntry
        {
            public string Id;
            public AudioClip[] Clips = System.Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 1f;
        }

        [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

        private Dictionary<string, SoundEntry> _lookup;

        private void OnEnable() => _lookup = null; // rebuild lazily; catches Inspector edits in the Editor too

        public bool TryGetEntry(string id, out SoundEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(id, out entry);
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, SoundEntry>();
            foreach (SoundEntry s in sounds)
            {
                if (s == null || string.IsNullOrEmpty(s.Id)) continue;
                _lookup[s.Id] = s; // last one wins on a duplicate id — a data mistake, not worth hard-failing an asset over
            }
        }

        /// <summary>
        /// Adds a blank row (empty Clips) for every id in <paramref name="ids"/>
        /// that isn't already present. Never touches an existing row, so a
        /// Tony hand-edit (clips assigned, volume tuned) survives every rerun
        /// of the editor tool that calls this — same create-once discipline as
        /// every other catalog in this project.
        /// </summary>
        public void EnsureIds(IEnumerable<string> ids)
        {
            var existing = new HashSet<string>();
            foreach (SoundEntry s in sounds)
                if (s != null && !string.IsNullOrEmpty(s.Id)) existing.Add(s.Id);

            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || existing.Contains(id)) continue;
                sounds.Add(new SoundEntry { Id = id });
                existing.Add(id);
            }

            _lookup = null;
        }
    }
}
