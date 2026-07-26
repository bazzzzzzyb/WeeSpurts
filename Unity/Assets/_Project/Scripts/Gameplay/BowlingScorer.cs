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
            int firstRollOfTenth = _rolls[FirstRollIndexOfFrame(9)];

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

        /// <summary>
        /// The LIVE score: everything earned so far, including the part of a
        /// pending strike/spare bonus that is already known.
        ///
        /// Why this exists alongside GetTotal(). A strike scores 10 + the next
        /// TWO rolls, so its frame box on a paper scorecard stays blank until
        /// those rolls happen — and GetTotal(), which only sums fully-resolved
        /// frames, therefore reads 0 for two more turns after a strike. Correct
        /// bookkeeping, useless scoreboard: you knocked ten pins down and the
        /// number said zero.
        ///
        /// This counts the pins immediately and folds each bonus in as soon as
        /// it is known, so the number only ever goes UP and lands on exactly the
        /// same value as GetTotal() once every frame has resolved. Use this for
        /// anything a player looks at; use GetTotal()/GetFrameTotals() for the
        /// formal scorecard boxes.
        /// </summary>
        public int GetProvisionalTotal()
        {
            int running = 0;
            int i = 0;

            for (int f = 0; f < 10 && i < _rolls.Count; f++)
            {
                if (f == 9)
                {
                    // The 10th frame has no NEXT frame to draw a bonus from —
                    // its bonus rolls are already part of the frame itself, so
                    // whatever has been rolled in it simply counts.
                    for (int r = i; r < _rolls.Count; r++) running += _rolls[r];
                    break;
                }

                if (_rolls[i] == 10) // strike: 10 + the next two rolls, as they arrive
                {
                    running += 10;
                    if (i + 1 < _rolls.Count) running += _rolls[i + 1];
                    if (i + 2 < _rolls.Count) running += _rolls[i + 2];
                    i += 1;
                }
                else if (i + 1 < _rolls.Count)
                {
                    int frameSum = _rolls[i] + _rolls[i + 1];
                    running += frameSum;
                    // Spare: 10 + the next one roll, once it exists.
                    if (frameSum == 10 && i + 2 < _rolls.Count) running += _rolls[i + 2];
                    i += 2;
                }
                else
                {
                    // First roll of a frame that is still in progress — the pins
                    // count right away even though the frame isn't finished.
                    running += _rolls[i];
                    i += 1;
                }
            }

            return running;
        }

        /// <summary>
        /// The rolls split up by frame, for DISPLAY only — so a scorecard can
        /// show "X" or "7 /" under the right box instead of one flat list.
        ///
        /// Lives here rather than in the HUD on purpose: working out where a
        /// frame's rolls start is exactly the walk GetFrameTotals() does (a
        /// strike takes one roll, anything else takes two, the 10th takes
        /// whatever is left). Duplicating that in UI code is how a scoreboard
        /// starts quietly disagreeing with the score. A frame not yet reached
        /// is null; a frame mid-roll has just the rolls thrown so far.
        /// </summary>
        public int[][] GetFrameRolls()
        {
            var frames = new int[10][];
            int i = 0;

            for (int f = 0; f < 10 && i < _rolls.Count; f++)
            {
                if (f == 9)
                {
                    // The 10th owns every remaining roll — up to three of them.
                    int count = _rolls.Count - i;
                    frames[9] = new int[count];
                    for (int r = 0; r < count; r++) frames[9][r] = _rolls[i + r];
                    break;
                }

                if (_rolls[i] == 10)
                {
                    frames[f] = new[] { 10 };
                    i += 1;
                }
                else if (i + 1 < _rolls.Count)
                {
                    frames[f] = new[] { _rolls[i], _rolls[i + 1] };
                    i += 2;
                }
                else
                {
                    // Frame in progress: only the first ball has been thrown.
                    frames[f] = new[] { _rolls[i] };
                    i += 1;
                }
            }

            return frames;
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

        /// <summary>
        /// Sandbox/debug: discards any rolls recorded so far in the CURRENT
        /// frame and re-racks it, without touching earlier frames' scores or
        /// advancing the frame index. Lets Tony retry one throw setup
        /// repeatedly instead of playing through pin counting every time.
        /// No-op once the game is over — use a full rematch there instead.
        /// </summary>
        public void ResetCurrentFrame()
        {
            if (IsGameOver) return;

            int firstRollIndex = FirstRollIndexOfFrame(CurrentFrame);
            _rolls.RemoveRange(firstRollIndex, _rolls.Count - firstRollIndex);

            RollInFrame = 0;
            PinsStanding = 10;
            NextRollNeedsFreshRack = true;
        }

        private int FirstRollIndexOfFrame(int frame)
        {
            // Walk the frames before it to find where its rolls start. Only
            // valid for a frame that has actually been reached, since it
            // assumes every earlier frame took its normal 1 (strike) or 2 rolls.
            int i = 0;
            for (int f = 0; f < frame; f++)
                i += (_rolls[i] == 10) ? 1 : 2;
            return i;
        }
    }
}
