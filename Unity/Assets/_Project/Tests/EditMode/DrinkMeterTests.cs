using NUnit.Framework;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Tests
{
    /// <summary>
    /// Unit tests for the pure-C# drink meter (Docs/Prompts/2026-08-11-
    /// executive-day-plan.md, Block 1 task 3).
    ///
    /// The headline properties these protect: PowerMeterSpeedMultiplier and
    /// GreenZoneWidthMultiplier only ever go DOWN as Level rises,
    /// AimSwayAmplitude only ever goes UP, and every output stays in a sane
    /// range no matter how hard Drink() is abused.
    /// </summary>
    public class DrinkMeterTests
    {
        private static DrinkTunables Tunables() => new DrinkTunables
        {
            DecayPerFrame = 0.01f,
            LooseThreshold = 0.25f,
            DrunkThreshold = 0.55f,
            GoneThreshold = 0.85f,
            MinPowerMeterSpeedMultiplier = 0.4f,
            MinGreenZoneWidthMultiplier = 0.3f,
            MaxAimSwayAmplitude = 15f
        };

        // ---------------------------------------------------------------
        // Basic level tracking
        // ---------------------------------------------------------------

        [Test]
        public void NewMeter_StartsAtZero_Sober()
        {
            var meter = new DrinkMeter(Tunables());
            Assert.AreEqual(0f, meter.Level);
            Assert.AreEqual(DrinkTier.Sober, meter.Tier);
        }

        [Test]
        public void Drink_RaisesLevel()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(0.3f);
            Assert.AreEqual(0.3f, meter.Level, 0.0001f);
        }

        [Test]
        public void Drink_Accumulates()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(0.2f);
            meter.Drink(0.2f);
            Assert.AreEqual(0.4f, meter.Level, 0.0001f);
        }

        [Test]
        public void Drink_ClampsAtOne_EvenWithAHugeAmount()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(1000f);
            Assert.AreEqual(1f, meter.Level);
        }

        [Test]
        public void Drink_ZeroOrNegative_IsIgnored()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(0.5f);
            meter.Drink(0f);
            meter.Drink(-10f);
            Assert.AreEqual(0.5f, meter.Level, 0.0001f);
        }

        // ---------------------------------------------------------------
        // Tiering
        // ---------------------------------------------------------------

        [Test]
        public void Tiering_MatchesTheConfiguredThresholds()
        {
            var meter = new DrinkMeter(Tunables());

            meter.Drink(0.1f);
            Assert.AreEqual(DrinkTier.Sober, meter.Tier);

            meter.Drink(0.2f); // Level 0.3
            Assert.AreEqual(DrinkTier.Loose, meter.Tier);

            meter.Drink(0.3f); // Level 0.6
            Assert.AreEqual(DrinkTier.Drunk, meter.Tier);

            meter.Drink(0.3f); // Level 0.9
            Assert.AreEqual(DrinkTier.Gone, meter.Tier);
        }

        [Test]
        public void Tiering_IsInclusiveAtItsLowerBound()
        {
            var t = Tunables();
            var meter = new DrinkMeter(t);

            meter.Drink(t.LooseThreshold);
            Assert.AreEqual(DrinkTier.Loose, meter.Tier, "landing exactly on the threshold should already read as the new tier");
        }

        // ---------------------------------------------------------------
        // Decay
        // ---------------------------------------------------------------

        [Test]
        public void Decay_LowersLevel()
        {
            var meter = new DrinkMeter(Tunables()); // DecayPerFrame 0.01
            meter.Drink(0.5f);
            meter.Decay(10f); // 10 frames * 0.01 = 0.1

            Assert.AreEqual(0.4f, meter.Level, 0.0001f);
        }

        [Test]
        public void Decay_EventuallyReachesExactlyZero_AndStopsThere()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(1f);

            for (int i = 0; i < 1000; i++) meter.Decay(1f);

            Assert.AreEqual(0f, meter.Level);
            Assert.AreEqual(DrinkTier.Sober, meter.Tier);

            meter.Decay(1f); // decaying past zero must not go negative
            Assert.AreEqual(0f, meter.Level);
        }

        [Test]
        public void Decay_ZeroOrNegativeFrames_IsIgnored()
        {
            var meter = new DrinkMeter(Tunables());
            meter.Drink(0.5f);
            meter.Decay(0f);
            meter.Decay(-5f);
            Assert.AreEqual(0.5f, meter.Level, 0.0001f);
        }

        // ---------------------------------------------------------------
        // Output monotonicity — the part the aim phase actually reads
        // ---------------------------------------------------------------

        [Test]
        public void PowerMeterSpeedMultiplier_NeverIncreasesAsLevelRises()
        {
            var meter = new DrinkMeter(Tunables());
            float previous = meter.PowerMeterSpeedMultiplier;

            for (int i = 0; i < 20; i++)
            {
                meter.Drink(0.05f);
                float current = meter.PowerMeterSpeedMultiplier;
                Assert.LessOrEqual(current, previous + 0.0001f, $"step {i}: speed multiplier went UP as Level rose");
                previous = current;
            }
        }

        [Test]
        public void GreenZoneWidthMultiplier_NeverIncreasesAsLevelRises()
        {
            var meter = new DrinkMeter(Tunables());
            float previous = meter.GreenZoneWidthMultiplier;

            for (int i = 0; i < 20; i++)
            {
                meter.Drink(0.05f);
                float current = meter.GreenZoneWidthMultiplier;
                Assert.LessOrEqual(current, previous + 0.0001f, $"step {i}: green-zone multiplier went UP as Level rose");
                previous = current;
            }
        }

        [Test]
        public void AimSwayAmplitude_NeverDecreasesAsLevelRises()
        {
            var meter = new DrinkMeter(Tunables());
            float previous = meter.AimSwayAmplitude;

            for (int i = 0; i < 20; i++)
            {
                meter.Drink(0.05f);
                float current = meter.AimSwayAmplitude;
                Assert.GreaterOrEqual(current, previous - 0.0001f, $"step {i}: sway amplitude went DOWN as Level rose");
                previous = current;
            }
        }

        [Test]
        public void AtZeroLevel_OutputsAreCompletelyUnaffected()
        {
            var meter = new DrinkMeter(Tunables());
            Assert.AreEqual(1f, meter.PowerMeterSpeedMultiplier);
            Assert.AreEqual(1f, meter.GreenZoneWidthMultiplier);
            Assert.AreEqual(0f, meter.AimSwayAmplitude);
        }

        [Test]
        public void AtFullLevel_OutputsHitTheConfiguredExtremes()
        {
            var t = Tunables();
            var meter = new DrinkMeter(t);
            meter.Drink(1f);

            Assert.AreEqual(t.MinPowerMeterSpeedMultiplier, meter.PowerMeterSpeedMultiplier, 0.0001f);
            Assert.AreEqual(t.MinGreenZoneWidthMultiplier, meter.GreenZoneWidthMultiplier, 0.0001f);
            Assert.AreEqual(t.MaxAimSwayAmplitude, meter.AimSwayAmplitude, 0.0001f);
        }

        // ---------------------------------------------------------------
        // Clamping at extreme / nonsense input
        // ---------------------------------------------------------------

        [Test]
        public void OutputsStaySane_EvenAfterManyHugeDrinks()
        {
            var meter = new DrinkMeter(Tunables());
            for (int i = 0; i < 50; i++) meter.Drink(1_000_000f);

            Assert.AreEqual(1f, meter.Level);
            Assert.GreaterOrEqual(meter.PowerMeterSpeedMultiplier, 0f);
            Assert.LessOrEqual(meter.PowerMeterSpeedMultiplier, 1f);
            Assert.GreaterOrEqual(meter.GreenZoneWidthMultiplier, 0f);
            Assert.LessOrEqual(meter.GreenZoneWidthMultiplier, 1f);
            Assert.GreaterOrEqual(meter.AimSwayAmplitude, 0f);
        }

        [Test]
        public void Tunables_Sanitize_FixesNonsenseInsteadOfCrashing()
        {
            var t = new DrinkTunables
            {
                DecayPerFrame = -1f,
                LooseThreshold = 0.9f,
                DrunkThreshold = 0.5f,   // out of order vs Loose
                GoneThreshold = 0.1f,    // out of order vs both
                MinPowerMeterSpeedMultiplier = 5f,   // above 1 — nonsense, meter would speed UP
                MinGreenZoneWidthMultiplier = -2f,   // negative — nonsense
                MaxAimSwayAmplitude = -10f
            };

            t.Sanitize();

            Assert.GreaterOrEqual(t.DecayPerFrame, 0f);
            Assert.LessOrEqual(t.LooseThreshold, t.DrunkThreshold);
            Assert.LessOrEqual(t.DrunkThreshold, t.GoneThreshold);
            Assert.GreaterOrEqual(t.MinPowerMeterSpeedMultiplier, 0.01f);
            Assert.LessOrEqual(t.MinPowerMeterSpeedMultiplier, 1f);
            Assert.GreaterOrEqual(t.MinGreenZoneWidthMultiplier, 0.01f);
            Assert.LessOrEqual(t.MinGreenZoneWidthMultiplier, 1f);
            Assert.GreaterOrEqual(t.MaxAimSwayAmplitude, 0f);
        }

        [Test]
        public void Meter_WithNullTunables_UsesSanitizedDefaultsRatherThanThrowing()
        {
            var meter = new DrinkMeter(null);
            Assert.DoesNotThrow(() => meter.Drink(0.5f));
            Assert.GreaterOrEqual(meter.PowerMeterSpeedMultiplier, 0f);
        }
    }
}
