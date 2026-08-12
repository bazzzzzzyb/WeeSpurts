using NUnit.Framework;
using WeeSpurts.Characters;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for AttachmentState — the slot-replacement logic behind
    /// AttachmentSlots.Equip/Unequip (Docs/CharacterPipeline.md §3 Layer 3).
    /// Run in Unity: Window > General > Test Runner > EditMode > Run All.
    ///
    /// AttachmentSlots itself needs a live Animator with a bound Humanoid
    /// avatar to spawn/parent anything, so it isn't exercised here (same call
    /// InventoryTests makes about its ScriptableObject-backed sibling). What
    /// IS testable without the editor's scene machinery is the pure struct
    /// logic AttachmentSlots delegates to for every mutation — "one item per
    /// slot, equipping replaces" lives entirely in <see cref="AttachmentState.With"/>.
    /// </summary>
    public class AttachmentStateTests
    {
        private const int HAT = 1;
        private const int BALL = 2;
        private const int VISOR = 3;

        [Test]
        public void None_IsBareAndEveryGetIsEmpty()
        {
            AttachmentState state = AttachmentState.None;

            Assert.IsTrue(state.IsBare);
            foreach (AttachmentSlot slot in System.Enum.GetValues(typeof(AttachmentSlot)))
                Assert.AreEqual(AttachmentState.Empty, state.Get(slot));
        }

        [Test]
        public void With_SetsOnlyTheGivenSlot()
        {
            AttachmentState state = AttachmentState.None.With(AttachmentSlot.Head, HAT);

            Assert.AreEqual(HAT, state.Get(AttachmentSlot.Head));
            Assert.AreEqual(AttachmentState.Empty, state.Get(AttachmentSlot.RightHand));
            Assert.AreEqual(AttachmentState.Empty, state.Get(AttachmentSlot.LeftHand));
            Assert.AreEqual(AttachmentState.Empty, state.Get(AttachmentSlot.Face));
            Assert.AreEqual(AttachmentState.Empty, state.Get(AttachmentSlot.Back));
            Assert.IsFalse(state.IsBare);
        }

        [Test]
        public void With_OnAnAlreadyOccupiedSlot_ReplacesRatherThanStacking()
        {
            AttachmentState state = AttachmentState.None
                .With(AttachmentSlot.Head, HAT)
                .With(AttachmentSlot.Head, VISOR);

            // Equipping replaces: the slot holds exactly one id, the new one —
            // this is the whole rule "one item per slot" reduces to.
            Assert.AreEqual(VISOR, state.Get(AttachmentSlot.Head));
        }

        [Test]
        public void With_DoesNotMutateTheOriginal()
        {
            AttachmentState original = AttachmentState.None;
            AttachmentState changed = original.With(AttachmentSlot.RightHand, BALL);

            // Struct value semantics: `With` must return a copy, not mutate
            // `this` in place, or two callers holding "the same" state would
            // silently diverge or converge depending on call order.
            Assert.AreEqual(AttachmentState.Empty, original.Get(AttachmentSlot.RightHand));
            Assert.AreEqual(BALL, changed.Get(AttachmentSlot.RightHand));
        }

        [Test]
        public void With_EmptyId_UnequipsThatSlot()
        {
            AttachmentState state = AttachmentState.None
                .With(AttachmentSlot.LeftHand, BALL)
                .With(AttachmentSlot.LeftHand, AttachmentState.Empty);

            Assert.IsTrue(state.IsEmpty(AttachmentSlot.LeftHand));
            Assert.IsTrue(state.IsBare);
        }

        [Test]
        public void With_DifferentSlots_AreIndependent()
        {
            AttachmentState state = AttachmentState.None
                .With(AttachmentSlot.Head, HAT)
                .With(AttachmentSlot.RightHand, BALL)
                .With(AttachmentSlot.LeftHand, VISOR);

            Assert.AreEqual(HAT, state.Get(AttachmentSlot.Head));
            Assert.AreEqual(BALL, state.Get(AttachmentSlot.RightHand));
            Assert.AreEqual(VISOR, state.Get(AttachmentSlot.LeftHand));
            Assert.IsTrue(state.IsEmpty(AttachmentSlot.Face));
            Assert.IsTrue(state.IsEmpty(AttachmentSlot.Back));
        }

        [Test]
        public void Equals_SameContents_AreEqual()
        {
            AttachmentState a = AttachmentState.None.With(AttachmentSlot.Head, HAT);
            AttachmentState b = AttachmentState.None.With(AttachmentSlot.Head, HAT);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_DifferentContents_AreNotEqual()
        {
            AttachmentState a = AttachmentState.None.With(AttachmentSlot.Head, HAT);
            AttachmentState b = AttachmentState.None.With(AttachmentSlot.Head, VISOR);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void SlotCount_MatchesTheEnum()
        {
            // Guards against the enum growing a member without SlotCount (and
            // therefore any code that loops 0..SlotCount) noticing.
            Assert.AreEqual(System.Enum.GetValues(typeof(AttachmentSlot)).Length, AttachmentState.SlotCount);
        }
    }
}
