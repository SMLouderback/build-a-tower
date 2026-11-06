using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class MarketClimateTests
    {
        [Test]
        public void Starts_at_Normal_with_matching_name_and_labels()
        {
            var climate = new MarketClimate();
            Assert.AreEqual(MarketClimate.Normal, climate.Step);
            Assert.AreEqual(2, climate.Step);
            Assert.AreEqual("Normal", climate.Name);
            CollectionAssert.AreEqual(
                new[] { "Recession", "Slow", "Normal", "Strong", "Boom" },
                MarketClimate.Labels);
        }

        [TestCase(MarketClimate.Recession, 0.7f)]
        [TestCase(MarketClimate.Slow, 0.85f)]
        [TestCase(MarketClimate.Normal, 1f)]
        [TestCase(MarketClimate.Strong, 1.15f)]
        [TestCase(MarketClimate.Boom, 1.3f)]
        public void SpendMultiplier_matches_step_table(int targetStep, float expected)
        {
            var climate = WalkToStep(targetStep);
            Assert.AreEqual(expected, climate.SpendMultiplier, 0.0001f);
        }

        [TestCase(MarketClimate.Recession, -2)]
        [TestCase(MarketClimate.Slow, -1)]
        [TestCase(MarketClimate.Normal, 0)]
        [TestCase(MarketClimate.Strong, 1)]
        [TestCase(MarketClimate.Boom, 2)]
        public void ComfortTierOffset_is_step_minus_normal(int targetStep, int expected)
        {
            var climate = WalkToStep(targetStep);
            Assert.AreEqual(expected, climate.ComfortTierOffset);
        }

        [Test]
        public void Many_month_rolls_stay_within_0_to_4()
        {
            var climate = new MarketClimate();
            var rng = new Random(42);
            for (var i = 0; i < 500; i++)
            {
                climate.OnMonthRolled(rng);
                Assert.GreaterOrEqual(climate.Step, 0);
                Assert.LessOrEqual(climate.Step, 4);
            }
        }

        [Test]
        public void Reflect_turns_recession_down_into_recovery()
        {
            Assert.AreEqual(MarketClimate.Slow, MarketClimate.Reflect(-1));
            Assert.AreEqual(MarketClimate.Normal, MarketClimate.Reflect(-2));
            Assert.AreEqual(MarketClimate.Strong, MarketClimate.Reflect(5));
            Assert.AreEqual(MarketClimate.Normal, MarketClimate.Reflect(6));
        }

        [Test]
        public void Down_roll_at_recession_bounces_toward_recovery()
        {
            var climate = WalkToStep(MarketClimate.Recession);
            // −2 at Recession reflects to Normal; suppress mean-reversion with high roll.
            climate.OnMonthRolled(new ScriptedRandom(DeltaMinus2Roll, 99));
            Assert.AreEqual(MarketClimate.Normal, climate.Step);
        }

        [Test]
        public void Up_roll_at_boom_bounces_toward_normal()
        {
            var climate = WalkToStep(MarketClimate.Boom);
            climate.OnMonthRolled(new ScriptedRandom(DeltaPlus2Roll, 99));
            Assert.AreEqual(MarketClimate.Normal, climate.Step);
        }

        [Test]
        public void Cannot_linger_at_recession_beyond_max_consecutive_months()
        {
            var climate = WalkToStep(MarketClimate.Recession);
            Assert.AreEqual(1, climate.MonthsAtCurrentStep);

            // One more stay is allowed (month 2 at Recession).
            climate.OnMonthRolled(new ScriptedRandom(DeltaStayRoll, 99));
            Assert.AreEqual(MarketClimate.Recession, climate.Step);
            Assert.AreEqual(2, climate.MonthsAtCurrentStep);

            // Third consecutive month must leave.
            climate.OnMonthRolled(new ScriptedRandom(DeltaStayRoll, 99));
            Assert.AreEqual(MarketClimate.Slow, climate.Step);
        }

        [Test]
        public void Long_random_walk_does_not_spend_majority_in_recession()
        {
            var climate = new MarketClimate();
            var rng = new Random(7);
            var recessionMonths = 0;
            const int months = 400;
            for (var i = 0; i < months; i++)
            {
                climate.OnMonthRolled(rng);
                if (climate.Step == MarketClimate.Recession)
                    recessionMonths++;
            }

            Assert.Less(recessionMonths, months / 3, "Recession should not dominate a long walk.");
        }

        // Weight bands for Next(100): stay 0–39, −1 40–61, +1 62–84, −2 85–91, +2 92–99
        const int DeltaStayRoll = 0;
        const int DeltaMinus2Roll = 85;
        const int DeltaPlus2Roll = 92;
        const int DeltaMinus1Roll = 40;
        const int DeltaPlus1Roll = 62;

        static MarketClimate WalkToStep(int targetStep)
        {
            var climate = new MarketClimate();
            // Mean-reversion second roll: 99 = skip nudge.
            while (climate.Step < targetStep)
                climate.OnMonthRolled(new ScriptedRandom(DeltaPlus1Roll, 99));
            while (climate.Step > targetStep)
                climate.OnMonthRolled(new ScriptedRandom(DeltaMinus1Roll, 99));
            Assert.AreEqual(targetStep, climate.Step);
            return climate;
        }

        /// <summary>
        /// Returns a fixed roll from <see cref="Random.Next(int)"/> for climate weight picks.
        /// </summary>
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
