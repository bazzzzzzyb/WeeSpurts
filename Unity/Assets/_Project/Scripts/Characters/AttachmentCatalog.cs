using System;
using UnityEngine;

namespace WeeSpurts.Characters
{
    /// <summary>
    /// One thing that can be equipped onto a bone — a hat, a held prop, the
    /// bowling ball in Block 6's carried-item system. Plain serializable data,
    /// same DATA/LOGIC split as <see cref="WeeSpurts.Gameplay.ItemCatalog"/>:
    /// this class and <see cref="AttachmentCatalog"/> are DATA,
    /// <see cref="CosmeticAttachment"/> and <see cref="AttachmentSlots"/> are
    /// the LOGIC and know nothing about what a given id looks like.
    ///
    /// BONE IS A FIELD HERE, NOT A CONSTANT SOMEWHERE ELSE, and offsets are
    /// baked in per-item rather than assumed to be zero, because different
    /// props need different alignment on the same bone — the ball sits in the
    /// palm, a drink sits pinched between two fingers Meshy never modelled, so
    /// its offset compensates by hand.
    /// </summary>
    [Serializable]
    public class AttachmentDefinition
    {
        [Tooltip("Stable, unique within this catalog. Sent over the network later — DO NOT renumber existing items once anyone has one; add new ids instead. 0 is reserved for AttachmentState.Empty.")]
        public int ItemId;

        [Tooltip("What menus/tooltips say. Display only — never switch logic on this string.")]
        public string DisplayName = "Attachment";

        [Tooltip("Instantiated and parented to Bone. Keep this small — MeshyDump/Cosmetics and MeshyDump/Items are 2M-tri exports awaiting re-export and must NOT be wired in here yet; use a placeholder until they land.")]
        public GameObject Prefab;

        [Tooltip("Fetched via Animator.GetBoneTransform on the wearer — bone-NAME-agnostic, so this survives any character body swap (Docs/CharacterPipeline.md Layer 3).")]
        public HumanBodyBones Bone = HumanBodyBones.RightHand;

        [Tooltip("Local position offset from the bone, applied after parenting.")]
        public Vector3 PositionOffset;

        [Tooltip("Local rotation offset from the bone, in Euler degrees (Inspector-friendly; converted to a Quaternion on attach).")]
        public Vector3 RotationOffsetEuler;

        [Tooltip("Local scale applied to the instance. (1,1,1) unless the prop needs resizing to read at the mascot's scale.")]
        public Vector3 ScaleOffset = Vector3.one;
    }

    /// <summary>
    /// Every attachment that exists, as an asset Tony can edit in the
    /// Inspector — same shape as <see cref="WeeSpurts.Gameplay.ItemCatalog"/>
    /// and <see cref="WeeSpurts.Slop.VendorConfig"/>. One catalog is shared by
    /// every character; which SLOT an id ends up in is a caller decision
    /// (<see cref="AttachmentSlots.Equip"/>), not stored here — the same
    /// bowling-ball id could be a RightHand carry today and a Back trophy
    /// later without a data change.
    /// </summary>
    [CreateAssetMenu(fileName = "AttachmentCatalog", menuName = "WeeSpurts/Attachment Catalog")]
    public class AttachmentCatalog : ScriptableObject
    {
        [Tooltip("Every attachment that exists. ItemIds must be unique — on a duplicate, the FIRST entry with that id wins (same rule ItemCatalog and Vendor use).")]
        public AttachmentDefinition[] Attachments = new AttachmentDefinition[0];

        /// <summary>
        /// Pure lookup, no Unity API beyond iterating the array — this is what
        /// makes it testable in EditMode without a scene or an Animator.
        /// </summary>
        public bool TryGetDefinition(int itemId, out AttachmentDefinition found)
        {
            for (int i = 0; i < Attachments.Length; i++)
            {
                if (Attachments[i] == null || Attachments[i].ItemId != itemId) continue;
                found = Attachments[i];
                return true;
            }

            found = null;
            return false;
        }
    }
}
