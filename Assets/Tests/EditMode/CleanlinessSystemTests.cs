using System;
using System.Linq;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CleanlinessSystemTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);

            GameSession.ResetForTests();
        }

        [Test]
        public void Target_is_100_when_nobody_is_home()
        {
            Assert.AreEqual(100f, CleanlinessSystem.Target(0, 0));
            Assert.AreEqual(100f, CleanlinessSystem.Target(12, 12));
            Assert.AreEqual(50f, CleanlinessSystem.Target(12, 24));
            Assert.AreEqual(0f, CleanlinessSystem.Target(0, 8));
            Assert.AreEqual(400f, CleanlinessSystem.Target(48, 12));
        }

        [Test]
        public void Step_closes_a_tenth_of_the_gap()
        {
            Assert.AreEqual(90f, CleanlinessSystem.Step(100f, 0f));
            Assert.AreEqual(81f, CleanlinessSystem.Step(90f, 0f));
            Assert.AreEqual(72.9f, CleanlinessSystem.Step(81f, 0f), 0.001f);
        }

        [Test]
        public void Effects_use_the_locked_slopes()
        {
            Assert.AreEqual(1f, CleanlinessSystem.Dirt(0f));
            Assert.AreEqual(0.5f, CleanlinessSystem.Dirt(50f));
            Assert.AreEqual(0f, CleanlinessSystem.Dirt(100f));
            Assert.AreEqual(0f, CleanlinessSystem.Dirt(140f));
            Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 0f));
            Assert.AreEqual(0.65f, CleanlinessSystem.FillMultiplier(LuxuryBand.Mid, 0f));
            Assert.AreEqual(0.35f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 0f));
            Assert.AreEqual(0.85f, CleanlinessSystem.FillMultiplier(LuxuryBand.None, 0f));
            Assert.AreEqual(0.925f, CleanlinessSystem.FillMultiplier(LuxuryBand.Base, 50f), 0.001f);
            Assert.AreEqual(1f, CleanlinessSystem.FillMultiplier(LuxuryBand.Upper, 140f));
            Assert.AreEqual(0.08f, CleanlinessSystem.IllnessPerMinute(0f));
            Assert.AreEqual(0.04f, CleanlinessSystem.IllnessPerMinute(50f));
            Assert.AreEqual(0f, CleanlinessSystem.IllnessPerMinute(100f));
            Assert.AreEqual(0.60f, CleanlinessSystem.RestaurantWeightFactor(0f));
            Assert.AreEqual(0.80f, CleanlinessSystem.RestaurantWeightFactor(50f));
            Assert.AreEqual(1f, CleanlinessSystem.RestaurantWeightFactor(140f));
        }

        [Test]
        public void Treatment_table_matches_the_pump_clock_shape()
        {
            CollectionAssert.AreEqual(new[] { 12, 20, 32, 48 },
                new[] { 0, 1, 2, 3 }.Select(CleanlinessSystem.TreatmentSupply).ToArray());
            Assert.AreEqual(30000, CleanlinessSystem.TreatmentUpgradeFee(1));
            Assert.AreEqual(70000, CleanlinessSystem.TreatmentUpgradeFee(2));
            Assert.AreEqual(140000, CleanlinessSystem.TreatmentUpgradeFee(3));
            Assert.AreEqual(0, CleanlinessSystem.TreatmentUpgradeFee(4));
            Assert.AreEqual(1440, CleanlinessSystem.TreatmentUpgradeMinutes(1));
            Assert.AreEqual(4320, CleanlinessSystem.TreatmentUpgradeMinutes(2));
            Assert.AreEqual(10080, CleanlinessSystem.TreatmentUpgradeMinutes(3));
        }

        [Test]
        public void Absent_snapshot_means_clean_and_a_saved_zero_stays_zero()
        {
            Assert.IsTrue(CleanlinessSystem.IsAbsent(null));
            Assert.IsTrue(CleanlinessSystem.IsAbsent(new CleanlinessSnapshotV1 { percent = 0f }));
            Assert.IsFalse(CleanlinessSystem.IsAbsent(new CleanlinessSnapshotV1 { present = true, percent = 0f }));
            Assert.AreEqual(0f, new CleanlinessSnapshotV1 { present = true, percent = 0f }.percent);
        }

        [Test]
        public void Legacy_json_without_cleanliness_block_is_absent_and_restores_clean()
        {
            var legacy = JsonUtility.FromJson<TowerSnapshotV1>("{\"schemaVersion\":1}");
            Assert.IsNotNull(legacy.cleanliness);
            Assert.IsTrue(CleanlinessSystem.IsAbsent(legacy.cleanliness));

            var build = CreateBuild(out var simulation);
            simulation.RestoreCleanliness(legacy.cleanliness);
            Assert.AreEqual(100f, simulation.Cleanliness);
        }

        [Test]
        public void JsonUtility_round_trip_saved_zero_cleanliness_stays_zero()
        {
            var build = CreateBuild(out var simulation);
            simulation.RestoreCleanliness(new CleanlinessSnapshotV1 { present = true, percent = 0f });
            var captured = TowerSnapshotMapper.Capture(
                "save_clean",
                "Tower",
                build,
                simulation,
                DateTime.UtcNow);

            var json = JsonUtility.ToJson(captured);
            var roundTripped = JsonUtility.FromJson<TowerSnapshotV1>(json);
            Assert.IsFalse(CleanlinessSystem.IsAbsent(roundTripped.cleanliness));

            simulation.RestoreCleanliness(null);
            simulation.RestoreCleanliness(roundTripped.cleanliness);
            Assert.AreEqual(0f, simulation.Cleanliness);
        }

        [Test]
        public void Capture_and_restore_round_trip_cleanliness_percent()
        {
            var build = CreateBuild(out var simulation);
            Assert.AreEqual(100f, simulation.Cleanliness);

            simulation.RestoreCleanliness(new CleanlinessSnapshotV1 { present = true, percent = 0f });
            var captured = TowerSnapshotMapper.Capture(
                "save_clean",
                "Tower",
                build,
                simulation,
                DateTime.UtcNow);
            Assert.IsNotNull(captured.cleanliness);
            Assert.IsTrue(captured.cleanliness.present);
            Assert.AreEqual(0f, captured.cleanliness.percent);

            simulation.RestoreCleanliness(null);
            Assert.AreEqual(100f, simulation.Cleanliness);

            simulation.RestoreCleanliness(new CleanlinessSnapshotV1 { present = true, percent = 0f });
            Assert.AreEqual(0f, simulation.Cleanliness);
        }

        static BuildController CreateBuild(out TowerSimulation simulation)
        {
            var gameObject = new GameObject("Cleanliness Test Tower");
            var build = gameObject.AddComponent<BuildController>();
            SetAutoProperty(build, "Grid", new TowerGrid());
            SetAutoProperty(build, "Wallet", new FundsWallet(100));
            simulation = gameObject.AddComponent<TowerSimulation>();
            SetField(simulation, "_clock", new GameClock());
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
    }
}
