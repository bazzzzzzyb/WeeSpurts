using System;

namespace WeeSpurts.Characters
{
    /// <summary>
    /// WHERE on a character an attachment lives, as a wardrobe rule rather
    /// than a physical location: one item per slot, equipping replaces.
    ///
    /// THIS IS NOT THE SAME THING AS THE BONE. The bone (a
    /// <see cref="UnityEngine.HumanBodyBones"/>) is where the prefab physically
    /// hangs and is a property of the ITEM, stored in
    /// <see cref="AttachmentDefinition"/>. The slot is what you are allowed to
    /// wear at once. They usually agree — a hat is slot Head on bone Head, the
    /// bowling ball is slot RightHand on bone RightHand — but keeping them
    /// separate is what lets a humiliation cosmetic put a bowling pin on the
    /// Back slot, or two different props share a hand without the catalog
    /// needing to know which hand is free.
    ///
    /// VALUES ARE EXPLICIT AND MUST NEVER BE RENUMBERED — same rule as item
    /// ids (Docs/SlopLayerPlan.md Rule 3). This goes over the wire as an int
    /// inside <see cref="AttachmentState"/>, so renumbering would silently
    /// move everyone's hat onto their back mid-session.
    /// </summary>
    public enum AttachmentSlot
    {
        Head = 0,
        Face = 1,
        RightHand = 2,
        LeftHand = 3,
        Back = 4
    }

    /// <summary>
    /// EVERYTHING a character is currently wearing or holding, as five ints.
    ///
    /// WHY A STRUCT OF INTS AND NOT A LIST OF PREFAB REFERENCES — this is
    /// Docs/CharacterPipeline.md §3 Layer 3's rule ("an attachment is
    /// (itemId, HumanBodyBones, offset), stored as ints and an enum, which
    /// replicates over Mirror for free. Never an object reference") and
    /// Docs/SlopLayerPlan.md Rule 3. A struct whose fields are all ints is
    /// something Mirror can generate a reader/writer for with no hand-written
    /// serializer, so the day this becomes a [SyncVar] on the player object,
    /// NOT ONE CALL SITE CHANGES: <see cref="AttachmentSlots"/> already routes
    /// every mutation through <see cref="With"/> and every application through
    /// <see cref="AttachmentSlots.ApplyState"/>, which is exactly the shape a
    /// SyncVar hook needs.
    ///
    /// Fields are named rather than an int[5] on purpose: an array inside a
    /// struct is a REFERENCE, so two copies of the state would share storage
    /// and value semantics would quietly be a lie — and Mirror cannot generate
    /// a writer for it either.
    ///
    /// <see cref="Empty"/> (0) means the slot is empty, which is why no real
    /// attachment may ever use id 0.
    /// </summary>
    [Serializable]
    public struct AttachmentState : IEquatable<AttachmentState>
    {
        /// <summary>The id that means "nothing equipped". No catalog entry may use it.</summary>
        public const int Empty = 0;

        /// <summary>How many slots exist. Derived from the enum so adding one can't leave this stale.</summary>
        public static readonly int SlotCount = Enum.GetValues(typeof(AttachmentSlot)).Length;

        public int Head;
        public int Face;
        public int RightHand;
        public int LeftHand;
        public int Back;

        /// <summary>Nothing equipped anywhere. `default(AttachmentState)` is the same thing — Empty is 0 by design.</summary>
        public static AttachmentState None => default;

        /// <summary>
        /// What is in one slot, or <see cref="Empty"/>. An out-of-range slot
        /// reads as empty rather than throwing: this is fed by network data
        /// later, and a peer sending a garbage slot must not be able to throw
        /// an exception inside our update loop.
        /// </summary>
        public int Get(AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Head: return Head;
                case AttachmentSlot.Face: return Face;
                case AttachmentSlot.RightHand: return RightHand;
                case AttachmentSlot.LeftHand: return LeftHand;
                case AttachmentSlot.Back: return Back;
                default: return Empty;
            }
        }

        /// <summary>
        /// A COPY of this state with one slot set — the whole of "equipping
        /// replaces". There is no Set: returning a new value rather than
        /// mutating in place is what keeps the struct safe to hand around,
        /// compare, and eventually shove through a SyncVar.
        ///
        /// Passing <see cref="Empty"/> unequips. An unknown slot returns the
        /// state unchanged, for the same don't-trust-the-wire reason as
        /// <see cref="Get"/>.
        /// </summary>
        public AttachmentState With(AttachmentSlot slot, int itemId)
        {
            AttachmentState copy = this;
            switch (slot)
            {
                case AttachmentSlot.Head: copy.Head = itemId; break;
                case AttachmentSlot.Face: copy.Face = itemId; break;
                case AttachmentSlot.RightHand: copy.RightHand = itemId; break;
                case AttachmentSlot.LeftHand: copy.LeftHand = itemId; break;
                case AttachmentSlot.Back: copy.Back = itemId; break;
            }
            return copy;
        }

        /// <summary>Convenience for the common "is anything in this slot" question.</summary>
        public bool IsEmpty(AttachmentSlot slot) => Get(slot) == Empty;

        /// <summary>True when nothing at all is equipped.</summary>
        public bool IsBare => Head == Empty && Face == Empty && RightHand == Empty
                              && LeftHand == Empty && Back == Empty;

        public bool Equals(AttachmentState other) =>
            Head == other.Head && Face == other.Face && RightHand == other.RightHand
            && LeftHand == other.LeftHand && Back == other.Back;

        public override bool Equals(object obj) => obj is AttachmentState other && Equals(other);

        public override int GetHashCode()
        {
            // Plain unchecked rolling hash — no HashCode.Combine, which is not
            // available on every scripting backend this project may target.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Head;
                hash = hash * 31 + Face;
                hash = hash * 31 + RightHand;
                hash = hash * 31 + LeftHand;
                hash = hash * 31 + Back;
                return hash;
            }
        }

        public override string ToString() =>
            $"Head={Head}, Face={Face}, RightHand={RightHand}, LeftHand={LeftHand}, Back={Back}";
    }
}
