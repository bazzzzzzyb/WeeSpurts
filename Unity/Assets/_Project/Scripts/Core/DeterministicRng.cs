namespace WeeSpurts.Core
{
    /// <summary>
    /// A tiny, explicit pseudo-random generator whose output is IDENTICAL on
    /// every machine, forever.
    ///
    /// WHY NOT System.Random: its algorithm is documented as implementation-
    /// defined, and it genuinely has changed between .NET runtimes. Today
    /// `BowlingBall` uses it for the throw wobble and the spike measured zero
    /// drift Mac-to-PC, so it is empirically fine on the runtimes you ship —
    /// this is NOT a bug report on that code. But a card game has no tolerance
    /// at all: a wobble that differs by a thousandth is invisible, whereas one
    /// different card means one player is looking at a completely different
    /// hand. When the cost of a mismatch is that high, "empirically fine" is
    /// the wrong standard. Sixteen lines of arithmetic we control is the right
    /// one.
    ///
    /// WHY NOT UnityEngine.Random: it is a global, shared, mutable generator.
    /// Anything else in the frame that draws from it changes your sequence.
    ///
    /// The algorithm is xorshift32 (Marsaglia). Not cryptographic — it is
    /// shuffling a deck of cards in a party game, not protecting anything.
    /// It is fast, has a period of 2^32-1, and is exactly reproducible from
    /// a seed, which is the only property that matters here.
    ///
    /// A STRUCT on purpose: each system holds its own generator and its own
    /// sequence, so the slot machine can never perturb the card shoe.
    /// </summary>
    public struct DeterministicRng
    {
        private uint _state;

        /// <summary>
        /// Seed it. Any int works, including negative ones and, importantly,
        /// zero — xorshift is dead at state 0 (it would return 0 forever), so
        /// that case is remapped to a fixed constant rather than silently
        /// producing a generator that isn't random at all.
        /// </summary>
        public DeterministicRng(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        /// <summary>Next raw 32-bit value. Never returns 0.</summary>
        public uint NextUInt()
        {
            // xorshift32: three shift-xor steps, constants 13/17/5.
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>
        /// Uniform integer in [0, maxExclusive).
        ///
        /// Uses REJECTION SAMPLING rather than a plain modulo. Plain modulo is
        /// subtly biased whenever the range doesn't divide 2^32 evenly — for a
        /// 52-card deck the bias is tiny, but "tiny and permanent" is how a
        /// deck ends up very slightly favouring low cards forever, and nobody
        /// would ever find it by playing. Rejecting the short tail costs one
        /// extra draw about once in 82 million and removes the bias entirely.
        /// </summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;

            uint range = (uint)maxExclusive;
            uint limit = uint.MaxValue - (uint.MaxValue % range) - 1;

            uint value;
            do
            {
                value = NextUInt();
            }
            while (value > limit);

            return (int)(value % range);
        }

        /// <summary>
        /// Fisher-Yates, in place. The ONLY shuffle used in this project —
        /// every other "shuffle" people write by accident (sorting by a random
        /// key, repeatedly swapping random pairs) is measurably not uniform.
        /// </summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> items)
        {
            if (items == null) return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
