using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeeSpurts.Characters
{
    /// <summary>
    /// What a character has equipped, and the only place that ever changes it.
    /// Docs/CharacterPipeline.md §3 Layer 3, wired onto an actual character:
    /// this is the "hats, held items, props" system, and per that doc's
    /// worked example, Block 6's bowling ball in the mascot's hand is a
    /// RightHand attachment through this exact component — not a parallel
    /// mechanism, per the task that created this file.
    ///
    /// STATE LIVES HERE AS AN <see cref="AttachmentState"/> (five ints), and
    /// every mutation goes through <see cref="Equip"/>/<see cref="Unequip"/>,
    /// which both update <see cref="State"/> and spawn/despawn the actual
    /// <see cref="CosmeticAttachment"/> GameObjects. That is what makes this
    /// Mirror-ready with no call-site changes later: a future [SyncVar] hook
    /// on the host just calls <see cref="ApplyState"/> with whatever arrived
    /// over the wire, and every client converges the same way Equip/Unequip
    /// already do locally.
    /// </summary>
    /// NOT [RequireComponent(typeof(Animator))], and the reason is a trap worth
    /// naming: the PlayerCharacter prefab is a WRAPPER root with the imported
    /// model as a CHILD, and the Animator lives on the child, not the root.
    /// RequireComponent would have "helpfully" added a SECOND, empty Animator
    /// to the root — no avatar, no bones — and GetComponent would then find
    /// that one first, so GetBoneTransform would return null for every bone and
    /// nothing would ever attach. Resolving from CHILDREN (the same thing
    /// CharacterThrowReactionActor does, for the same prefab layout) is what
    /// makes this work on the real prefab.
    public class AttachmentSlots : MonoBehaviour
    {
        [Tooltip("Where item ids resolve to prefab/bone/offset. Shared across every character, same as ItemCatalog — assign the one AttachmentCatalog asset.")]
        public AttachmentCatalog Catalog;

        [Tooltip("Animator whose Humanoid avatar supplies the bones. Left empty, it's found on this object or its children at Awake — which is the normal case, since the PlayerCharacter prefab keeps its Animator on the model CHILD, not the root.")]
        [SerializeField] private Animator animator;

        private Animator _animator;

        // Slot -> the live instance currently occupying it, so Unequip/replace
        // knows exactly what to tear down without searching the hierarchy.
        private readonly Dictionary<AttachmentSlot, CosmeticAttachment> _active =
            new Dictionary<AttachmentSlot, CosmeticAttachment>();

        private static readonly AttachmentSlot[] AllSlots =
            (AttachmentSlot[])Enum.GetValues(typeof(AttachmentSlot));

        /// <summary>The current loadout as five ints. Read this to know what's equipped; never write it directly — go through Equip/Unequip/ApplyState.</summary>
        public AttachmentState State { get; private set; }

        private void Awake()
        {
            // GetComponentInChildren also checks THIS object, so it covers both
            // the wrapper-root prefab layout and a bare model root — identical
            // resolution to CharacterThrowReactionActor.Awake. See the class
            // doc for why this is children-inclusive rather than RequireComponent.
            _animator = animator != null ? animator : GetComponentInChildren<Animator>();

            if (_animator == null)
                Debug.LogWarning($"[AttachmentSlots] {name} found no Animator on itself or its children — " +
                                 "nothing will ever attach. Assign one, or put this component on the " +
                                 "PlayerCharacter prefab root (the model child carries the Animator).");
        }

        /// <summary>
        /// Puts <paramref name="itemId"/> in <paramref name="slot"/>. ONE ITEM
        /// PER SLOT — whatever was there is unequipped first, so this is safe
        /// to call blind without checking the slot yourself.
        ///
        /// Returns false (and logs, doesn't throw) if the catalog is missing,
        /// the id isn't in it, or the definition's bone doesn't exist on this
        /// avatar — see <see cref="CosmeticAttachment.Attach"/>. On a false
        /// return the slot ends up EMPTY, not left holding the old item: a
        /// failed equip should never look like a silent no-op.
        /// </summary>
        public bool Equip(AttachmentSlot slot, int itemId)
        {
            if (Catalog == null)
            {
                Debug.LogWarning($"[AttachmentSlots] {name} has no AttachmentCatalog assigned — cannot equip item {itemId}.");
                return false;
            }

            if (!Catalog.TryGetDefinition(itemId, out AttachmentDefinition definition))
            {
                Debug.LogWarning($"[AttachmentSlots] {name}: item {itemId} is not in the catalog — nothing equipped in {slot}.");
                Unequip(slot);
                return false;
            }

            // Equipping replaces: tear down whatever was here before spawning
            // the new one, so a slot never silently holds two instances.
            Unequip(slot);

            CosmeticAttachment spawned = CosmeticAttachment.Attach(_animator, definition);
            if (spawned == null) return false; // already logged by Attach

            _active[slot] = spawned;
            State = State.With(slot, itemId);
            return true;
        }

        /// <summary>Clears a slot. Safe to call on an already-empty slot.</summary>
        public void Unequip(AttachmentSlot slot)
        {
            if (_active.TryGetValue(slot, out CosmeticAttachment existing) && existing != null)
                existing.Detach();
            _active.Remove(slot);

            State = State.With(slot, AttachmentState.Empty);
        }

        /// <summary>
        /// Converges every slot to match <paramref name="incoming"/> — the
        /// entry point a future Mirror SyncVar hook calls with whatever state
        /// just arrived from the host. Diffs against the CURRENT state rather
        /// than tearing everything down and rebuilding it, so a change to one
        /// slot only respawns that one attachment.
        /// </summary>
        public void ApplyState(AttachmentState incoming)
        {
            foreach (AttachmentSlot slot in AllSlots)
            {
                int want = incoming.Get(slot);
                if (want == State.Get(slot)) continue;

                if (want == AttachmentState.Empty) Unequip(slot);
                else Equip(slot, want);
            }
        }
    }
}
