using UnityEngine;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Loops one clip at one point in space, forever, once the clip exists.
    /// The birthday room and the DJ booth each need their own always-on music
    /// bed, and neither should go through <see cref="AudioManager.SetAmbience"/>
    /// — that's ONE global slot, already claimed by the bowling alley's own
    /// ambience, and two rooms sharing it would mean whichever set it last
    /// wins. Same AddComponent&lt;AudioSource&gt;-in-Awake, loop=true,
    /// spatialBlend=1 pattern <see cref="Bowling.BowlingBall"/> already uses
    /// for its roll loop, just reusable instead of copy-pasted a second time.
    ///
    /// ROOMS ISOLATE BY DISTANCE (Tony's call): <see cref="minDistance"/>/
    /// <see cref="maxDistance"/> are set explicitly rather than left at
    /// Unity's own AudioSource defaults (min 1m, max 500m) — 500m is
    /// enormous next to a ~30x42m venue, so left alone the DJ booth and the
    /// birthday room would both be faintly audible almost everywhere,
    /// exactly the opposite of "each room has its own bed." Linear rolloff,
    /// not Logarithmic: Logarithmic trails off slowly and unpredictably at
    /// this scale, where Linear gives a clean, tunable "full volume out to
    /// minDistance, silent past maxDistance" — the actual shape a room needs.
    ///
    /// SETUP: [SerializeField] soundId, wired by an editor tool same as every
    /// other station field in this project. Plays automatically once
    /// AudioManager.Instance exists and has a clip for that id — silent
    /// (never an exception) if either isn't true yet.
    /// </summary>
    public class PositionalAmbience : MonoBehaviour
    {
        [Tooltip("Which AudioCatalog row to loop here. Empty means silent.")]
        [SerializeField] private string soundId;

        [Tooltip("Meters. Full volume anywhere inside this radius.")]
        [SerializeField] private float minDistance = 3f;

        [Tooltip("Meters. Silent at or beyond this radius — this is the actual room-isolation knob. Tune per-instance in the Inspector if one room should carry further/less far than another.")]
        [SerializeField] private float maxDistance = 15f;

        private AudioSource _source;
        private bool _started;

        /// <summary>Editor-tool wiring, same convention as every other config field on this project.</summary>
        public void SetSoundId(string id) => soundId = id;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = minDistance;
            _source.maxDistance = maxDistance;
        }

        private void Update()
        {
            // AudioManager.Instance may not exist yet the frame this wakes up
            // (scene load order isn't guaranteed relative to GameManager's own
            // Awake) — retry each frame until it does, then start once and
            // stop checking. A missing clip (id unassigned, or the row exists
            // but is empty) is a permanent, not a transient, "nothing to
            // play" — so _started still latches true rather than retrying
            // forever for a clip that will never arrive.
            if (_started || string.IsNullOrEmpty(soundId) || AudioManager.Instance == null) return;

            _started = true;
            AudioClip clip = AudioManager.Instance.GetClip(soundId);
            if (clip == null) return;

            _source.clip = clip;
            _source.Play();
        }
    }
}
