using System;
using System.Collections.Generic;

namespace WeeSpurts.Gameplay
{
    /// <summary>What happened as a result of the last roll.</summary>
    public enum RollOutcome
    {
        FrameContinues,   // same player rolls again this frame
        FrameComplete,    // frame over — pass the turn
        GameComplete      // this player's 10 frames are done
    }

    /// <summary>
    /// Pure C# ten-pin bowling scorer. Deliberately NOT a MonoBehaviour:
    /// no Unity types at all, so it can be unit-tested in the Test Runner
    /// without ever entering Play mode, and later run identically on any
    /// networked client (determinism matters — see Docs/Networking.md).
    ///
    /// Rules implemented: 10 frames; strike = 10 + next two rolls;
    /// spare = 10 + next roll; 10th frame grants up to 3 rolls when a
    /// strike or spare is thrown in it.
    /// </summary>
    public class BowlingScorer
    {
        private readonly List<int> _rolls = new List<int>();

        /// <summary>0-based frame index, 0..9.</summary>
        public int CurrentFrame { get; private set; }

        /// <summary>0-based roll index within the current frame.</summary>
        public int RollInFrame { get; private set; }

        /// <summary>Pins currently standing for the next roll.</summary>
        public int PinsStanding { get; private set; } = 10;

        public bool IsGameOver { get; private set; }

        /// <summary>
        /// True when the next roll should start from a full, fresh rack of 10
        /// (new frame, or a strike/spare rack reset in the 10th frame).
        /// The game controller uses this to decide whether to reset the pins.
        /// </summary>
        public bool NextRollNeedsFreshRack { get; private set; } = true;

        /// <summary>All rolls so far (read-only) — handy for UI and debugging.</summary>
        public IReadOnlyList<int> Rolls => _rolls;

        /// <summary>
        /// Record a roll. Returns what the game should do next.
        /// Throws if the roll is impossible (more pins than are standing).
        /// </summary>
        public RollOutcome AddRoll(int pinsKnocked)
        {
            if (IsGameOver)
                throw new InvalidOperationException("Game is over; no more rolls.");
            if (pinsKnocked < 0 || pinsKnocked > PinsStanding)
                throw new ArgumentOutOfRangeException(nameof(pinsKnocked),
                    $"Knocked {pinsKnocked} but only {PinsStanding} pins were standing.");

            _rolls.Add(pinsKnocked);
            PinsStanding -= pinsKnocked;

            if (CurrentFrame < 9)
                return AdvanceNormalFrame();
            return AdvanceTenthFrame();
        }

        private RollOutcome AdvanceNormalFrame()
        {
            bool strike = RollInFrame == 0 && PinsStanding == 0;
            bool secondRollDone = RollInFrame == 1;

            if (strike || secondRollDone)
            {
                CurrentFrame++;
                RollInFrame = 0;
                PinsStanding = 10;
                NextRollNeedsFreshRack = true;
                return RollOutcome.FrameComplete;
            }

            RollInFrame = 1;
            NextRollNeedsFreshRack = false; // second roll at the leftover pins
            return RollOutcome.FrameContinues;
        }

        private RollOutcome AdvanceTenthFrame()
        {
            // The 10th frame is the weird one: strikes/spares earn bonus rolls,
            // and the rack resets whenever all 10 go down.
            int firstRollOfTenth = _rolls[FirstRollIndexOfTenth()];

            if (RollInFrame == 0)
            {
                if (PinsStanding == 0) { PinsStanding = 10; NextRollNeedsFreshRack = true; } // strike
                else NextRollNeedsFreshRack = false;
                RollInFrame = 1;
                return RollOutcome.FrameContinues;
            }

            if (RollInFrame == 1)
            {
                bool earnedBonus = firstRollOfTenth == 10 || PinsStanding == 0; // strike earlier, or spare now
                if (PinsStanding == 0) { PinsStanding = 10; NextRollNeedsFreshRack = true; }
                else NextRollNeedsFreshRack = false;

                if (earnedBonus)
                {
                    RollInFrame = 2;
                    return RollOutcome.FrameContinues;
                }
                IsGameOver = true;
                return RollOutcome.GameComplete;
            }

            // Third roll taken — always the end.
            IsGameOver = true;
            return RollOutcome.GameComplete;
        }

        private int FirstRollIndexOfTenth()
        {
            // Walk frames 1-9 to find where the 10th frame's rolls start.
            int i = 0;
            for (int f = 0; f < 9; f++)
                i += (_rolls[i] == 10) ? 1 : 2;
            return i;
        }

        /// <summary>
        /// Running totals per frame, standard scorecard style. A frame whose
        /// bonus isn't resolvable yet (waiting on future rolls) is null —
        /// exactly like the blank box on a real scorecard.
        /// </summary>
        public int?[] GetFrameTotals()
        {
            var totals = new int?[10];
            int running = 0;
            int i = 0;

            for (int f = 0; f < 10; f++)
            {
                if (i >= _rolls.Count) break;

                if (f == 9)
                {
                    if (!IsGameOver) break; // 10th unresolved until all its rolls exist
                    int sum = 0;
                    for (int r = i; r < _rolls.Count; r++) sum += _rolls[r];
                    running += sum;
                    totals[9] = running;
                    break;
                }

                if (_rolls[i] == 10) // strike: needs the next two rolls
                {
                    if (i + 2 >= _rolls.Count) break;
                    running += 10 + _rolls[i + 1] + _rolls[i + 2];
                    totals[f] = running;
                    i += 1;
                }
                else if (i + 1 < _rolls.Count)
                {
                    int frameSum = _rolls[i] + _rolls[i + 1];
                    if (frameSum == 10) // spare: needs the next one roll
                    {
                        if (i + 2 >= _rolls.Count) break;
                        running += 10 + _rolls[i + 2];
                    }
                    else
                    {
                        running += frameSum;
                    }
                    totals[f] = running;
                    i += 2;
                }
                else break; // open frame, second roll not thrown yet
            }
            return totals;
        }

        /// <summary>Best-known total so far (last resolved frame's running total).</summary>
        public int GetTotal()
        {
            var totals = GetFrameTotals();
            int best = 0;
            for (int f = 0; f < 10; f++)
                if (totals[f].HasValue) best = totals[f].Value;
            return best;
        }
    }
}
