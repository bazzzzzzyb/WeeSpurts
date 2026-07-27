using UnityEngine;

namespace WeeSpurts.Interaction
{
    /// <summary>
    /// Every tunable for "can I use that thing I'm looking at". A
    /// ScriptableObject rather than constants in <see cref="PlayerInteractor"/>
    /// so Tony can retune reach and generosity in the Inspector, in Play mode,
    /// without a recompile (CLAUDE.md: "config/tunables are ScriptableObjects,
    /// not hard-coded constants").
    ///
    /// These three numbers together decide how FORGIVING interaction feels,
    /// which is a feel call and therefore Tony's, made by playing. A short
    /// range plus a narrow cone reads as precise and slightly fussy; a long
    /// range plus a wide cone reads as generous and occasionally targets the
    /// wrong thing in a crowded room. The venue is going to get crowded (bar,
    /// slots, card table, cosmetics counter), so expect to retune this once
    /// there is more than one interactable within a few metres of another.
    ///
    /// SETUP: RoamingSetupTool creates
    /// Assets/_Project/ScriptableObjects/InteractionConfig.asset on first run
    /// and never overwrites your tuning after that (same create-once rule as
    /// RoamConfig / Wobbler / Nuke).
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "WeeSpurts/Interaction Config")]
    public class InteractionConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Metres. How close your FEET have to be to an interactable's interaction point before it will offer itself. Measured as plain 3D distance from the player root, so a kiosk one floor level down (the settee pits are recessed 0.4m) still counts. 2m is roughly 'arm's length plus a step' at this game's scale — a 0.6m-wide body walking at 3.2 m/s.")]
        public float Range = 2f;

        [Tooltip("Degrees. The HALF-angle of the cone in front of your view that an interactable has to fall inside. 75 is deliberately generous — it means anything in the forward 150 degrees counts, so you don't have to centre it perfectly, but something directly behind you can never steal focus. Drop it towards 30 if the venue gets crowded and the wrong thing keeps winning; raise it towards 90 if aiming at things feels fussy.")]
        [Range(1f, 90f)] public float FacingAngleDegrees = 75f;

        [Header("Input")]
        [Tooltip("The key that triggers the thing you're looking at. E is the near-universal first-person 'use' key, so it needs no teaching. Changing it here changes both the behaviour AND the on-screen prompt, because the prompt text reads this same asset — nothing hard-codes the letter.")]
        public KeyCode InteractKey = KeyCode.E;
    }
}
