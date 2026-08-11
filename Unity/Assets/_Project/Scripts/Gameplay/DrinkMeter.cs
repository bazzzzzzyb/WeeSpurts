namespace WeeSpurts.Gameplay
{
    /// <summary>Named bands over <see cref="DrinkMeter.Level"/>, for the UI and for anything that wants a coarse read rather than the raw float.</summary>
    public enum DrinkTier { Sober, Loose, Drunk, Gone }

    /// <summary>
    /// Every knob on the drink meter, as plain C# so it can be unit-tested
    /// without Unity. <see cref="DrinkConfig"/> is the ScriptableObject that
    /// lets Tony edit these in the Inspector — same data/logic split as
    /// every other tunable set in this project.
    /// </summary>
    public class DrinkTunables
    {
        /// <summary>How much <see cref="DrinkMeter.Level"/> drains per frame of <see cref="DrinkMeter.Decay"/>.</summary>
        public float DecayPerFrame = 0.0005f;

        public float LooseThreshold = 0.25f;
        public float DrunkThreshold = 0.55f;
        public float GoneThreshold = 0.85f;

        /// <summary>Power-meter fill speed at Level=1 (fully Gone). 1 means unaffected; this must stay in (0, 1] since the meter only ever gets SLOWER, never faster.</summary>
        public float MinPowerMeterSpeedMultiplier = 0.4f;

        /// <summary>Green-zone width at Level=1. Same shape as <see cref="MinPowerMeterSpeedMultiplier"/> — the window only ever narrows.</summary>
        public float MinGreenZoneWidthMultiplier = 0.3f;

        /// <summary>Aim sway amplitude at Level=1. Unit is whatever the aim-phase reader (e.g. ThrowerAimSlide) interprets it as; this class never touches that reader.</summary>
        public float MaxAimSwayAmplitude = 15f;

        /// <summary>
        /// Clamp anything nonsensical an Inspector edit could produce — a
        /// tier threshold out of order, a multiplier above 1 (which would
        /// mean drinking makes you BETTER), a negative decay. Called by
        /// <see cref="DrinkMeter"/> on construction, same contract as
        /// <see cref="WeeSpurts.Slop.BlackjackRules.Sanitize"/>.
        /// </summary>
        public void Sanitize()
        {
            if (DecayPerFrame < 0f) DecayPerFrame = 0f;

            LooseThreshold = Clamp01(LooseThreshold);
            DrunkThreshold = Clamp01(DrunkThreshold);
            GoneThreshold = Clamp01(GoneThreshold);

            // Force strictly increasing thresholds so the tier lookup never
            // has to guess which one "wins" on a misconfigured overlap.
            if (DrunkThreshold < LooseThreshold) DrunkThreshold = LooseThreshold;
            if (GoneThreshold < DrunkThreshold) GoneThreshold = DrunkThreshold;

            MinPowerMeterSpeedMultiplier = Clamp(MinPowerMeterSpeedMultiplier, 0.01f, 1f);
            MinGreenZoneWidthMultiplier = Clamp(MinGreenZoneWidthMultiplier, 0.01f, 1f);
            if (MaxAimSwayAmplitude < 0f) MaxAimSwayAmplitude = 0f;
        }

        private static float Clamp01(float v) => Clamp(v, 0f, 1f);
        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>
    /// ONE player's intoxication as a number and a curve — nothing more. Pure
    /// C#, no Unity: it never touches the ball, the lane, or the Animator, on
    /// purpose. `BarStation` grants drinks; whatever drives the aim phase
    /// (`ThrowerAimSlide`, the power meter) reads
    /// <see cref="PowerMeterSpeedMultiplier"/>/<see cref="GreenZoneWidthMultiplier"/>/
    /// <see cref="AimSwayAmplitude"/> and decides what to DO with them. This
    /// class deliberately does not know either of those systems exist —
    /// same reasoning `Vendor` doesn't know what a drink DOES.
    ///
    /// TWO OUTPUTS GO DOWN, ONE GOES UP, ALL BY DESIGN (Tony's 2026-08-11
    /// executive-day call, recorded in `Docs/GameBible.md`): the power meter
    /// filling SLOWER is a help — more time to hit the window — and the
    /// narrower green zone is what pays for that help, so drinking becomes a
    /// real trade-off instead of a pure debuff. Sway going up is the
    /// straightforward cost on top.
    /// </summary>
    public class DrinkMeter
    {
        private readonly DrinkTunables _tunables;

        /// <summary>0 (stone sober) to 1 (as drunk as this game gets). Always in range — see <see cref="Drink"/>/<see cref="Decay"/>.</summary>
        public float Level { get; private set; }

        public DrinkTier Tier
        {
            get
            {
                if (Level >= _tunables.GoneThreshold) return DrinkTier.Gone;
                if (Level >= _tunables.DrunkThreshold) return DrinkTier.Drunk;
                if (Level >= _tunables.LooseThreshold) return DrinkTier.Loose;
                return DrinkTier.Sober;
            }
        }

        /// <summary>1 at Level=0, down to <see cref="DrinkTunables.MinPowerMeterSpeedMultiplier"/> at Level=1. The meter fills slower as you get drunker.</summary>
        public float PowerMeterSpeedMultiplier => Clamp01(Lerp(1f, _tunables.MinPowerMeterSpeedMultiplier, Level));

        /// <summary>1 at Level=0, down to <see cref="DrinkTunables.MinGreenZoneWidthMultiplier"/> at Level=1. The perfect window narrows as you get drunker.</summary>
        public float GreenZoneWidthMultiplier => Clamp01(Lerp(1f, _tunables.MinGreenZoneWidthMultiplier, Level));

        /// <summary>0 at Level=0, up to <see cref="DrinkTunables.MaxAimSwayAmplitude"/> at Level=1. How much the aim wanders.</summary>
        public float AimSwayAmplitude => ClampNonNegative(Lerp(0f, _tunables.MaxAimSwayAmplitude, Level));

        public DrinkMeter(DrinkTunables tunables)
        {
            _tunables = tunables ?? new DrinkTunables();
            _tunables.Sanitize();
        }

        /// <summary>Take a drink. Negative or zero amounts are simply ignored — this is not a way to sober up.</summary>
        public void Drink(float amount)
        {
            if (amount <= 0f) return;
            Level = Clamp01(Level + amount);
        }

        /// <summary>Sober up over time. <paramref name="deltaFrames"/> is elapsed frames, not seconds — the caller owns the conversion from its own timestep.</summary>
        public void Decay(float deltaFrames)
        {
            if (deltaFrames <= 0f) return;
            Level = Clamp01(Level - _tunables.DecayPerFrame * deltaFrames);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float ClampNonNegative(float v) => v < 0f ? 0f : v;
    }
}
