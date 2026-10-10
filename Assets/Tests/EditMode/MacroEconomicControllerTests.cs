using System;
using System.Collections.Generic;
using System.Reflection;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class MacroEconomicControllerTests
    {
        [Test]
        public void Starts_at_neutral_with_first_pulse_on_day_7()
        {
            var macro = new MacroEconomicController(new Random(1));
            Assert.AreEqual(MacroEconomicController.NeutralPulseMult, macro.LivingPulseMult, 0.0001f);
            Assert.AreEqual(MacroEconomicController.NeutralPulseMult, macro.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(MacroEconomicController.PulsePeriodDays, macro.NextPulseDayIndex);
        }

        [Test]
        public void Tick_before_day_7_does_not_change_pulse()
        {
            var macro = new MacroEconomicController(new Random(2));
            var clock = new GameClock(1f, 0);
            clock.AdvanceMinutes(6 * GameClock.MinutesPerDay);
            Assert.AreEqual(6, clock.DayIndex);

            macro.Tick(clock);

            Assert.AreEqual(1f, macro.LivingPulseMult, 0.0001f);
            Assert.AreEqual(1f, macro.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(7, macro.NextPulseDayIndex);
        }

        // Weight bands for Next(100): stay 0–39, −step 40–61, +step 62–84, −2× 85–91, +2× 92–99
        const int DeltaPlus1Roll = 62;
        const int SkipMeanReversionRoll = 99;

        [Test]
        public void Tick_on_day_7_advances_pulse_and_schedules_next_week()
        {
            // Living/commercial each: Next(100) for step, then Next(100) for mean-reversion skip.
            // Step roll 62 → +MaxStepAbs; mean-reversion 99 → skip.
            var macro = new MacroEconomicController(new ScriptedRandom(
                DeltaPlus1Roll, SkipMeanReversionRoll,
                DeltaPlus1Roll, SkipMeanReversionRoll));
            var clock = ClockAtDay(7);

            macro.Tick(clock);

            Assert.AreEqual(1f + MacroEconomicController.MaxStepAbs, macro.LivingPulseMult, 0.0001f);
            Assert.AreEqual(1f + MacroEconomicController.MaxStepAbs, macro.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(14, macro.NextPulseDayIndex);
        }

        [Test]
        public void Tick_catch_up_rolls_once_per_elapsed_week()
        {
            var macro = new MacroEconomicController(new ScriptedRandom(
                DeltaPlus1Roll, SkipMeanReversionRoll, DeltaPlus1Roll, SkipMeanReversionRoll, // week 1
                DeltaPlus1Roll, SkipMeanReversionRoll, DeltaPlus1Roll, SkipMeanReversionRoll, // week 2
                DeltaPlus1Roll, SkipMeanReversionRoll, DeltaPlus1Roll, SkipMeanReversionRoll  // week 3
            ));
            var clock = ClockAtDay(21);

            macro.Tick(clock);

            var expected = 1f + 3f * MacroEconomicController.MaxStepAbs;
            Assert.AreEqual(expected, macro.LivingPulseMult, 0.0001f);
            Assert.AreEqual(expected, macro.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(28, macro.NextPulseDayIndex);
        }

        [Test]
        public void Pulse_multipliers_stay_clamped_across_many_rolls()
        {
            var macro = new MacroEconomicController(new Random(42));
            var clock = new GameClock(1f, 0);
            for (var week = 0; week < 200; week++)
            {
                clock.AdvanceMinutes(MacroEconomicController.PulsePeriodDays * GameClock.MinutesPerDay);
                macro.Tick(clock);
                Assert.GreaterOrEqual(macro.LivingPulseMult, MacroEconomicController.MinPulseMult);
                Assert.LessOrEqual(macro.LivingPulseMult, MacroEconomicController.MaxPulseMult);
                Assert.GreaterOrEqual(macro.CommercialPulseMult, MacroEconomicController.MinPulseMult);
                Assert.LessOrEqual(macro.CommercialPulseMult, MacroEconomicController.MaxPulseMult);
            }
        }

        [Test]
        public void Clamp_bounds_match_documented_range()
        {
            Assert.AreEqual(0.70f, MacroEconomicController.Clamp(0.1f), 0.0001f);
            Assert.AreEqual(1.30f, MacroEconomicController.Clamp(2f), 0.0001f);
            Assert.AreEqual(1f, MacroEconomicController.Clamp(1f), 0.0001f);
        }

        [Test]
        public void Snapshot_round_trip_preserves_pulse_state()
        {
            var macro = new MacroEconomicController(new Random(3));
            macro.ForcePulse(0.85f, 1.15f, nextPulseDayIndex: 21);

            var snapshot = JsonRoundTrip(macro.CaptureSnapshot());
            var restored = new MacroEconomicController(new Random(4));
            restored.RestoreSnapshot(snapshot, dayIndex: 18);

            Assert.AreEqual(0.85f, restored.LivingPulseMult, 0.0001f);
            Assert.AreEqual(1.15f, restored.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(21, restored.NextPulseDayIndex);
        }

        [Test]
        public void Null_snapshot_restores_neutral_aligned_to_day()
        {
            var macro = new MacroEconomicController(new Random(5));
            macro.ForcePulse(1.2f, 0.8f, nextPulseDayIndex: 99);

            macro.RestoreSnapshot(null, dayIndex: 10);

            Assert.AreEqual(1f, macro.LivingPulseMult, 0.0001f);
            Assert.AreEqual(1f, macro.CommercialPulseMult, 0.0001f);
            Assert.AreEqual(14, macro.NextPulseDayIndex);
        }

        [Test]
        public void Invalid_snapshot_throws()
        {
            var macro = new MacroEconomicController();
            var bad = new MarketPulseSnapshotV1
            {
                livingPulseMult = 0.5f,
                commercialPulseMult = 1f,
                nextPulseDayIndex = 7
            };
            Assert.Throws<ArgumentException>(() => macro.RestoreSnapshot(bad, dayIndex: 0));
        }

        [Test]
        public void Tower_snapshot_mapper_captures_and_validates_pulse()
        {
            var build = CreateBuild(out var simulation);
            try
            {
                var macro = new MacroEconomicController();
                macro.ForcePulse(0.9f, 1.1f, 14);
                SetField(simulation, "_macro", macro);

                var captured = TowerSnapshotMapper.Capture(
                    "save-pulse",
                    "Pulse Tower",
                    build,
                    simulation,
                    DateTime.UtcNow);

                Assert.IsNotNull(captured.marketPulse);
                Assert.AreEqual(0.9f, captured.marketPulse.livingPulseMult, 0.0001f);
                Assert.AreEqual(1.1f, captured.marketPulse.commercialPulseMult, 0.0001f);
                Assert.AreEqual(14, captured.marketPulse.nextPulseDayIndex);

                var ok = TowerSnapshotMapper.ValidateForRestore(captured);
                Assert.IsTrue(ok.Success, ok.ErrorMessage);

                captured.marketPulse.livingPulseMult = 0.1f;
                var bad = TowerSnapshotMapper.ValidateForRestore(captured);
                Assert.IsFalse(bad.Success);
                Assert.AreEqual(SnapshotValidationError.InvalidMarketPulse, bad.ErrorCode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(build.gameObject);
            }
        }

        static GameClock ClockAtDay(int dayIndex)
        {
            var clock = new GameClock(1f, 0);
            if (dayIndex > 0)
                clock.AdvanceMinutes(dayIndex * GameClock.MinutesPerDay);
            Assert.AreEqual(dayIndex, clock.DayIndex);
            return clock;
        }

        static MarketPulseSnapshotV1 JsonRoundTrip(MarketPulseSnapshotV1 snapshot) =>
            UnityEngine.JsonUtility.FromJson<MarketPulseSnapshotV1>(
                UnityEngine.JsonUtility.ToJson(snapshot));

        static BuildController CreateBuild(out TowerSimulation simulation)
        {
            var gameObject = new UnityEngine.GameObject("MacroPulse Test Tower");
            var build = gameObject.AddComponent<BuildController>();
            SetAutoProperty(build, "Grid", new TowerGrid());
            SetAutoProperty(build, "Wallet", new FundsWallet(100));
            simulation = gameObject.GetComponent<TowerSimulation>() ??
                         gameObject.AddComponent<TowerSimulation>();
            if (simulation.Clock == null)
                SetField(simulation, "_clock", new GameClock());
            if (simulation.Stars == null)
                SetField(simulation, "_stars", new StarSystem());
            return build;
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing test setup field {fieldName}.");
            field.SetValue(target, value);
        }

        /// <summary>Fixed <see cref="Random.Next(int)"/> sequence for pulse step / mean-reversion rolls.</summary>
        sealed class ScriptedRandom : Random
        {
            readonly Queue<int> _rolls;

            public ScriptedRandom(params int[] rolls)
            {
                _rolls = new Queue<int>(rolls);
            }

            public override int Next(int maxValue)
            {
                if (_rolls.Count == 0)
                    throw new InvalidOperationException("ScriptedRandom exhausted.");
                var roll = _rolls.Dequeue();
                if (roll < 0 || roll >= maxValue)
                    throw new ArgumentOutOfRangeException(nameof(maxValue), roll, "Scripted roll outside range.");
                return roll;
            }
        }
    }
}
