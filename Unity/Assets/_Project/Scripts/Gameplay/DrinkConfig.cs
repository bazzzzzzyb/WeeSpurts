using UnityEngine;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// The drink meter's knobs, as an asset Tony can edit in the Inspector.
    /// Same data/logic split as every other config in this project: this is
    /// DATA, <see cref="DrinkTunables"/> is the plain-C# mirror
    /// <see cref="DrinkMeter"/> actually runs on, and <see cref="CreateTunables"/>
    /// is the only bridge.
    ///
    /// Tony's design call (recorded in `Docs/GameBible.md`): the meter
    /// filling slower is a HELP and the narrowed green zone is the COST that
    /// pays for it — net difficulty should land roughly neutral at Loose and
    /// clearly worse at Gone. These two curves are the dial for that balance,
    /// meant to be tuned by playing, not by reasoning about it in the abstract.
    /// </summary>
    [CreateAssetMenu(fileName = "DrinkConfig", menuName = "WeeSpurts/Drink Config")]
    public class DrinkConfig : ScriptableObject
    {
        [Header("Decay")]
        [Tooltip("How much Level drains per frame of DrinkMeter.Decay. The caller passes elapsed frames, not seconds.")]
        public float DecayPerFrame = 0.0005f;

        [Header("Tiers")]
        [Range(0f, 1f)] public float LooseThreshold = 0.25f;
        [Range(0f, 1f)] public float DrunkThreshold = 0.55f;
        [Range(0f, 1f)] public float GoneThreshold = 0.85f;

        [Header("Aim-phase outputs — the help")]
        [Tooltip("Power meter fill speed at Level=1 (fully Gone). 1 = unaffected. Below 1 because a slower meter gives more time to hit the window.")]
        [Range(0.01f, 1f)] public float MinPowerMeterSpeedMultiplier = 0.4f;

        [Header("Aim-phase outputs — the cost")]
        [Tooltip("Green-zone width at Level=1. 1 = unaffected. This is what pays for the slower meter above.")]
        [Range(0.01f, 1f)] public float MinGreenZoneWidthMultiplier = 0.3f;

        [Tooltip("Aim sway amplitude at Level=1. Unit is whatever the aim-phase reader interprets it as — this asset never touches that reader.")]
        public float MaxAimSwayAmplitude = 15f;

        /// <summary>Snapshot these settings into the plain-C# tunables the meter runs on.</summary>
        public DrinkTunables CreateTunables() => new DrinkTunables
        {
            DecayPerFrame = DecayPerFrame,
            LooseThreshold = LooseThreshold,
            DrunkThreshold = DrunkThreshold,
            GoneThreshold = GoneThreshold,
            MinPowerMeterSpeedMultiplier = MinPowerMeterSpeedMultiplier,
            MinGreenZoneWidthMultiplier = MinGreenZoneWidthMultiplier,
            MaxAimSwayAmplitude = MaxAimSwayAmplitude
        };
    }
}
