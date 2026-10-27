using System;
using System.Globalization;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class MetroSnapshotTests
    {
        [Test]
        public void StreetSpawnChance_composes_metro_with_atrium_and_weather()
        {
            const int stars = 2;
            const float atrium = 1.15f;
            const float weather = 0.8f;
            const float metro = 1.16f;

            var withoutMetro = AgentSystem.StreetSpawnChance(stars, atrium, weather);
            var withMetro = AgentSystem.StreetSpawnChance(stars, atrium, weather, metro);
            var neutralMetro = AgentSystem.StreetSpawnChance(stars, atrium, weather, 1f);

            Assert.AreEqual(withoutMetro * metro, withMetro, 0.0001f);
            Assert.AreEqual(withoutMetro, neutralMetro, 0.0001f);
            Assert.AreEqual(0f, AgentSystem.StreetSpawnChance(stars, atrium, weather, metroMultiplier: 0f));
            Assert.AreEqual(0f, AgentSystem.StreetSpawnChance(stars, atrium, weather, metroMultiplier: -0.5f));
        }

        [Test]
        public void ElevatorWaitStressMultiplier_divides_full_wait_by_travel_relief()
        {
            var metro = new MetroSystem();
            metro.RegisterStation(new RectInt(0, -3, 8, 3));
            Assert.AreEqual(1.05f, metro.TravelReliefMultiplier, 0.0001f);

            var baseline = AgentSystem.ElevatorWaitStressMultiplier(
                AgentSystem.ElevatorWaitStressFullMinutes,
                travelReliefMultiplier: 1f);
            var relieved = AgentSystem.ElevatorWaitStressMultiplier(
                AgentSystem.ElevatorWaitStressFullMinutes,
                metro.TravelReliefMultiplier);

            Assert.AreEqual(AgentSystem.ElevatorWaitStressMaxMult, baseline, 0.0001f);
            Assert.AreEqual(baseline / metro.TravelReliefMultiplier, relieved, 0.0001f);
            Assert.AreEqual(
                0f,
                AgentSystem.ElevatorWaitStressMultiplier(
                    AgentSystem.ElevatorWaitStressStartMinutes,
                    metro.TravelReliefMultiplier));
        }

        [Test]
        public void Round_trip_preserves_station_footprints_and_tunnel_flag()
        {
            var metro = new MetroSystem();
            metro.RegisterStation(new RectInt(4, -3, 8, 3));
            metro.RegisterStation(new RectInt(28, -6, 8, 3));

            var restored = new MetroSystem();
            restored.RegisterStation(new RectInt(0, 0, 1, 1));
            restored.RestoreSnapshot(JsonRoundTrip(metro.CaptureSnapshot()));

            Assert.AreEqual(2, restored.StationCount);
            Assert.IsTrue(restored.HasTunnel);
            Assert.AreEqual(metro.StreetTrafficMultiplier, restored.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(metro.TravelReliefMultiplier, restored.TravelReliefMultiplier, 0.0001f);

            var again = restored.CaptureSnapshot();
            Assert.IsTrue(again.hasTunnel);
            Assert.AreEqual(2, again.stations.Length);
            Assert.AreEqual(4, again.stations[0].x);
            Assert.AreEqual(-3, again.stations[0].y);
            Assert.AreEqual(8, again.stations[0].width);
            Assert.AreEqual(3, again.stations[0].height);
            Assert.AreEqual(28, again.stations[1].x);
            Assert.AreEqual(-6, again.stations[1].y);
        }

        [Test]
        public void Null_or_absent_snapshot_restores_empty_metro()
        {
            var metro = new MetroSystem();
            metro.RegisterStation(new RectInt(0, -3, 8, 3));

            metro.RestoreSnapshot(null);

            Assert.AreEqual(0, metro.StationCount);
            Assert.IsFalse(metro.HasTunnel);
            Assert.AreEqual(1f, metro.StreetTrafficMultiplier, 0.0001f);
            Assert.AreEqual(1f, metro.TravelReliefMultiplier, 0.0001f);

            metro.RegisterStation(new RectInt(0, -3, 8, 3));
            var legacy = JsonUtility.FromJson<TowerSnapshotV1>("{\"schemaVersion\":1}");
            Assert.IsTrue(MetroSystem.IsAbsent(legacy.metro));
            metro.RestoreSnapshot(legacy.metro);

            Assert.AreEqual(0, metro.StationCount);
            Assert.IsFalse(metro.HasTunnel);
        }

        [Test]
        public void TryValidateSnapshot_rejects_inconsistent_tunnel_and_bad_footprints()
        {
            Assert.IsTrue(MetroSystem.TryValidateSnapshot(new MetroSnapshotV1
            {
                stations = Array.Empty<MetroStationFootprintV1>(),
                hasTunnel = false
            }, out _));

            var badSize = new MetroSnapshotV1
            {
                stations = new[]
                {
                    new MetroStationFootprintV1 { x = 0, y = -3, width = 0, height = 3 }
                },
                hasTunnel = true
            };
            Assert.IsFalse(MetroSystem.TryValidateSnapshot(badSize, out _));

            var tunnelWithoutStations = new MetroSnapshotV1
            {
                stations = Array.Empty<MetroStationFootprintV1>(),
                hasTunnel = true
            };
            Assert.IsFalse(MetroSystem.TryValidateSnapshot(tunnelWithoutStations, out _));

            var stationsWithoutTunnel = new MetroSnapshotV1
            {
                stations = new[]
                {
                    new MetroStationFootprintV1 { x = 0, y = -3, width = 8, height = 3 }
                },
                hasTunnel = false
            };
            Assert.IsFalse(MetroSystem.TryValidateSnapshot(stationsWithoutTunnel, out _));
        }

        [Test]
        public void Mapper_capture_includes_metro_and_validate_accepts_null_for_legacy_saves()
        {
            var build = CreateBuild(out var simulation);
            try
            {
                simulation.Metro.RegisterStation(new RectInt(12, -3, 8, 3));

                var captured = TowerSnapshotMapper.Capture(
                    "save-1",
                    "Tower",
                    build,
                    simulation,
                    DateTime.UtcNow);

                Assert.IsNotNull(captured.metro);
                Assert.IsTrue(captured.metro.hasTunnel);
                Assert.AreEqual(1, captured.metro.stations.Length);
                Assert.AreEqual(12, captured.metro.stations[0].x);
                Assert.AreEqual(-3, captured.metro.stations[0].y);
                Assert.AreEqual(8, captured.metro.stations[0].width);
                Assert.AreEqual(3, captured.metro.stations[0].height);

                var restored = new MetroSystem();
                restored.RestoreSnapshot(captured.metro);
                Assert.AreEqual(1, restored.StationCount);
                Assert.IsTrue(restored.HasTunnel);
                Assert.AreEqual(simulation.Metro.StreetTrafficMultiplier, restored.StreetTrafficMultiplier, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(build.gameObject);
            }

            var snapshot = ValidTowerSnapshot();
            snapshot.metro = null;
            Assert.IsTrue(TowerSnapshotMapper.ValidateForRestore(snapshot).Success);

            snapshot.metro = new MetroSnapshotV1
            {
                stations = Array.Empty<MetroStationFootprintV1>(),
                hasTunnel = true
            };
            var result = TowerSnapshotMapper.ValidateForRestore(snapshot);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(SnapshotValidationError.InvalidMetro, result.ErrorCode);
        }

        static MetroSnapshotV1 JsonRoundTrip(MetroSnapshotV1 snapshot) =>
            JsonUtility.FromJson<MetroSnapshotV1>(JsonUtility.ToJson(snapshot));

        static TowerSnapshotV1 ValidTowerSnapshot() => new TowerSnapshotV1
        {
            schemaVersion = SaveSchema.CurrentVersion,
            saveId = "save-1",
            towerName = "Tower",
            difficulty = nameof(GameDifficulty.Normal),
            modifiedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            walletBalance = 0,
            stars = 0,
            clock = new ClockSnapshotV1
            {
                dayIndex = 0,
                minuteOfDay = 0,
                minuteAccumulator = 0f,
                minutesPerRealSecond = 1f,
                paused = false
            },
            rooms = Array.Empty<RoomSnapshotV1>()
        };

        static BuildController CreateBuild(out TowerSimulation simulation)
        {
            var gameObject = new GameObject("MetroSnapshot Test Tower");
            var build = gameObject.AddComponent<BuildController>();
            SetAutoProperty(build, "Grid", new TowerGrid());
            SetAutoProperty(build, "Wallet", new FundsWallet(100));
            simulation = gameObject.GetComponent<TowerSimulation>() ??
                         gameObject.AddComponent<TowerSimulation>();
            if (simulation.Clock == null)
                SetField(simulation, "_clock", new GameClock());
            if (simulation.Stars == null)
                SetField(simulation, "_stars", new StarSystem());
            if (simulation.Metro == null)
                SetField(simulation, "_metro", new MetroSystem());
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
