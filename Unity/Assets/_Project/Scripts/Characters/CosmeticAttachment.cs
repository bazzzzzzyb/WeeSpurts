using UnityEngine;

namespace WeeSpurts.Characters
{
    /// <summary>
    /// One instantiated, bone-parented attachment — a hat, the bowling ball
    /// in Block 6's hand, a drink, a mop. Marks the instance so
    /// <see cref="AttachmentSlots"/> can identify and tear it down later; the
    /// actual spawning happens in the static <see cref="Attach"/> factory,
    /// which is the whole implementation of Docs/CharacterPipeline.md §3
    /// Layer 3: parent to a bone fetched by ENUM, never by name.
    /// </summary>
    public class CosmeticAttachment : MonoBehaviour
    {
        /// <summary>The catalog id this instance renders. Set by <see cref="Attach"/>, read-only after.</summary>
        public int ItemId { get; private set; }

        /// <summary>Which bone this is parented to — informational; the parenting itself already happened.</summary>
        public HumanBodyBones Bone { get; private set; }

        /// <summary>
        /// Instantiates <paramref name="definition"/>'s prefab and parents it
        /// to the bone <paramref name="animator"/> resolves for
        /// <see cref="AttachmentDefinition.Bone"/>.
        ///
        /// BONE-NAME-AGNOSTIC BY CONSTRUCTION: Animator.GetBoneTransform takes
        /// the HumanBodyBones enum, not a string, so this has no idea whether
        /// the underlying rig calls it "RightHand", "hand_r", or anything
        /// else. Swap the character body (Docs/CharacterPipeline.md Layer 2)
        /// and every attachment still finds its bone, because Humanoid
        /// retargeting guarantees the enum resolves the same way on any
        /// Humanoid avatar.
        ///
        /// Returns null (and logs, rather than throwing) if the animator has
        /// no avatar, the bone didn't map, or the definition has no prefab —
        /// all reachable at runtime from ordinary data mistakes, none of them
        /// a reason to crash a party game. A missing FINGER or TOE bone is
        /// expected on this mascot and is the caller's business, not this
        /// method's — RightHand/Head/Spine etc. are all required Humanoid
        /// bones and will always resolve on a valid avatar.
        /// </summary>
        public static CosmeticAttachment Attach(Animator animator, AttachmentDefinition definition)
        {
            if (animator == null)
            {
                Debug.LogWarning("[CosmeticAttachment] No Animator supplied — nothing attached.");
                return null;
            }
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning($"[CosmeticAttachment] Attachment definition for item " +
                                  $"{(definition != null ? definition.ItemId.ToString() : "?")} has no prefab — nothing attached.");
                return null;
            }

            Transform bone = animator.GetBoneTransform(definition.Bone);
            if (bone == null)
            {
                Debug.LogWarning($"[CosmeticAttachment] {animator.gameObject.name}'s avatar has no " +
                                  $"{definition.Bone} bone — item {definition.ItemId} ('{definition.DisplayName}') " +
                                  "was not attached. Expected for finger/toe bones on this mascot; anything else " +
                                  "means the avatar's Humanoid mapping needs a look (WeeSpurts > 1 Assets > Set Up Player Character logs it).");
                return null;
            }

            GameObject instance = Object.Instantiate(definition.Prefab, bone);
            instance.name = definition.DisplayName;
            instance.transform.localPosition = definition.PositionOffset;
            instance.transform.localRotation = Quaternion.Euler(definition.RotationOffsetEuler);
            instance.transform.localScale = definition.ScaleOffset;

            // Prefabs built specifically as attachments may already carry this
            // component (none do yet); a plain placeholder cube won't, so add
            // it rather than require every prefab author to remember to.
            CosmeticAttachment attachment = instance.GetComponent<CosmeticAttachment>();
            if (attachment == null) attachment = instance.AddComponent<CosmeticAttachment>();
            attachment.ItemId = definition.ItemId;
            attachment.Bone = definition.Bone;
            return attachment;
        }

        /// <summary>Tears this attachment down. A named method rather than a bare Destroy() call at every call site, so the "how" lives in one place.</summary>
        public void Detach()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
