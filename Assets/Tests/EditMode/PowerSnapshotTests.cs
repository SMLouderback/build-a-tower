using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BuildATower.Tests
{
    public class PowerSnapshotTests
    {
        [TearDown]
        public void TearDownLoadHost()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);
            GameSession.ResetForTests();
        }
        static RoomTypeSO PlantType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "service_power";
            type.size = new Vector2Int(8, 1);
            return type;
        }

        static RoomInstance Plant()
        {
            return new RoomInstance(7, PlantType(), new Vector2Int(0, -1), new Vector2Int(8, 1));
        }

        static RoomSnapshotV1 MinimalSnapshot()
        {
            return new RoomSnapshotV1
            {
                instanceId = 7,
                roomTypeId = "service_power",
                originX = 0,
                originY = -1,
                width = 8,
                height = 1
            };
        }

        [Test]
        public void Capture_restore_round_trips_utility_level_upgrade_clock_and_maintenance()
        {
            var room = Plant();
            room.UtilityLevel = 2;
            room.UpgradeMinutesRemaining = 100f;
            room.UpgradeRunning = true;
            room.PlayerMaintenance = true;

            var snapshot = room.CaptureSnapshot(nowRealtime: 0f);
            Assert.AreEqual(2, snapshot.utilityLevel);
            Assert.AreEqual(100f, snapshot.upgradeMinutesRemaining);
            Assert.IsTrue(snapshot.upgradeRunning);
            Assert.IsTrue(snapshot.playerMaintenance);

            var restored = Plant();
            restored.RestoreSnapshot(snapshot, nowRealtime: 0f);

            Assert.AreEqual(2, restored.UtilityLevel);
            Assert.AreEqual(100f, restored.UpgradeMinutesRemaining);
            Assert.IsTrue(restored.UpgradeRunning);
            Assert.IsTrue(restored.PlayerMaintenance);
        }

        [Test]
        public void Snapshot_without_utility_fields_restores_defaults()
        {
            var room = Plant();
            room.UtilityLevel = 3;
            room.UpgradeMinutesRemaining = 500f;
            room.UpgradeRunning = true;
            room.PlayerMaintenance = true;

            room.RestoreSnapshot(MinimalSnapshot(), nowRealtime: 0f);

            Assert.AreEqual(0, room.UtilityLevel);
            Assert.AreEqual(0f, room.UpgradeMinutesRemaining);
            Assert.IsFalse(room.UpgradeRunning);
            Assert.IsFalse(room.PlayerMaintenance);
        }

        [Test]
        public void Powered_is_not_written_to_snapshot()
        {
            var room = Plant();
            room.Powered = true;

            var snapshot = room.CaptureSnapshot(nowRealtime: 0f);
            var restored = Plant();
            restored.RestoreSnapshot(snapshot, nowRealtime: 0f);

            Assert.IsFalse(restored.Powered);
        }

        [Test]
        public void Old_save_without_utility_fields_runs_power_pass_after_load()
        {
            var go = new GameObject("PowerSnapshotLoadHost");
            var build = go.AddComponent<BuildController>();
            build.InitializeTower();
            var sim = go.GetComponent<TowerSimulation>() ?? go.AddComponent<TowerSimulation>();
            sim.InitializeSimulation();
            var grid = build.Grid;

            Assert.IsTrue(grid.TryPlaceLobby(LobbyTypeForLoad(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(ElevatorTypeForLoad(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryPlace(HotelTypeForLoad(), new Vector2Int(4, 1), out var hotel));
            for (var x = 0; x < 8; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, -1), out _));
            Assert.IsTrue(grid.TryPlace(PlantTypeForLoad(), new Vector2Int(0, -1), out var plant));
            sim.Router.Rebuild(grid);

            plant.UtilityLevel = 3;
            plant.UpgradeMinutesRemaining = 500f;
            plant.UpgradeRunning = true;
            plant.PlayerMaintenance = true;
            elevator.PlayerMaintenance = true;
            plant.RestoreSnapshot(LegacyRoomSnapshot(plant), nowRealtime: 0f);
            elevator.RestoreSnapshot(LegacyRoomSnapshot(elevator), nowRealtime: 0f);

            sim.Power();

            Assert.AreEqual(0, plant.UtilityLevel);
            Assert.AreEqual(0f, plant.UpgradeMinutesRemaining);
            Assert.IsFalse(plant.UpgradeRunning);
            Assert.IsFalse(plant.PlayerMaintenance);
            Assert.IsFalse(elevator.PlayerMaintenance);
            Assert.IsTrue(hotel.Powered, "Post-restore power pass should assign powered rooms before fill.");
        }

        static RoomTypeSO LobbyTypeForLoad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO ElevatorTypeForLoad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator";
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.size = new Vector2Int(1, 2);
            return so;
        }

        static RoomTypeSO HotelTypeForLoad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel_base";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO PlantTypeForLoad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_power";
            so.category = RoomCategory.Service;
            so.buildFamily = BuildFamily.Utility;
            so.allowBasement = true;
            so.allowAboveGround = false;
            so.size = new Vector2Int(8, 1);
            return so;
        }

        static RoomSnapshotV1 LegacyRoomSnapshot(RoomInstance room)
        {
            return new RoomSnapshotV1
            {
                instanceId = room.InstanceId,
                roomTypeId = room.Type.id,
                originX = room.Origin.x,
                originY = room.Origin.y,
                width = room.Size.x,
                height = room.Size.y
            };
        }
    }
}
