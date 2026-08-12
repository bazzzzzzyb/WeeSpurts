using NUnit.Framework;
using UnityEngine;
using WeeSpurts.Characters;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for AttachmentCatalog.TryGetDefinition — the Layer 3 data
    /// lookup (Docs/CharacterPipeline.md §3). Run in Unity: Window > General
    /// > Test Runner > EditMode > Run All.
    ///
    /// Deliberately does NOT touch CosmeticAttachment or AttachmentSlots:
    /// both need a live Animator with a bound Humanoid avatar to do anything
    /// meaningful (GetBoneTransform, actual instantiation), which isn't
    /// producible in a pure EditMode test without a real imported rig. Only
    /// the pure-data lookup is asserted here, same scoping call InventoryTests
    /// makes for ItemCatalog.
    /// </summary>
    public class AttachmentCatalogTests
    {
        private const int HAT = 1;
        private const int BALL = 2;
        private const int UNKNOWN = 999;

        private static AttachmentCatalog Catalog(params AttachmentDefinition[] items)
        {
            var catalog = ScriptableObject.CreateInstance<AttachmentCatalog>();
            catalog.Attachments = items;
            return catalog;
        }

        [Test]
        public void TryGetDefinition_KnownId_ReturnsIt()
        {
            var catalog = Catalog(
                new AttachmentDefinition { ItemId = HAT, DisplayName = "Hat", Bone = HumanBodyBones.Head },
                new AttachmentDefinition { ItemId = BALL, DisplayName = "Ball", Bone = HumanBodyBones.RightHand });

            bool found = catalog.TryGetDefinition(BALL, out AttachmentDefinition definition);

            Assert.IsTrue(found);
            Assert.AreEqual("Ball", definition.DisplayName);
            Assert.AreEqual(HumanBodyBones.RightHand, definition.Bone);
        }

        [Test]
        public void TryGetDefinition_UnknownId_ReturnsFalse()
        {
            var catalog = Catalog(new AttachmentDefinition { ItemId = HAT });

            bool found = catalog.TryGetDefinition(UNKNOWN, out AttachmentDefinition definition);

            Assert.IsFalse(found);
            Assert.IsNull(definition);
        }

        [Test]
        public void TryGetDefinition_EmptyCatalog_ReturnsFalseRatherThanThrowing()
        {
            var catalog = Catalog();

            Assert.IsFalse(catalog.TryGetDefinition(HAT, out _));
        }

        [Test]
        public void TryGetDefinition_DuplicateIds_FirstEntryWins()
        {
            var first = new AttachmentDefinition { ItemId = HAT, DisplayName = "First Hat" };
            var second = new AttachmentDefinition { ItemId = HAT, DisplayName = "Second Hat" };
            var catalog = Catalog(first, second);

            catalog.TryGetDefinition(HAT, out AttachmentDefinition found);

            Assert.AreSame(first, found);
        }

        [Test]
        public void TryGetDefinition_NullEntryInArray_IsSkippedNotThrown()
        {
            var catalog = Catalog(null, new AttachmentDefinition { ItemId = BALL });

            bool found = catalog.TryGetDefinition(BALL, out AttachmentDefinition definition);

            Assert.IsTrue(found);
            Assert.AreEqual(BALL, definition.ItemId);
        }
    }
}
