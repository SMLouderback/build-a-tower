using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class TowerGridRestoreTests
    {
        [Test]
        public void Round_trip_rebuilds_every_room_with_exact_ids_and_geometry()
        {
            var types = new TowerTypes();
            var source = BuildFullTower(types);
            var registry = types.Registry(source);
            var expectedCells = Fingerprint(source);

            var restored = new TowerGrid();
            var result = restored.RestoreRooms(source.CaptureRooms(nowRealtime: 40f), registry, 40f);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(GridRestoreError.None, result.ErrorCode);
            Assert.AreEqual(source.Rooms.Count, restored.Rooms.Count);
            for (var i = 0; i < source.Rooms.Count; i++)
            {
                var expected = source.Rooms[i];
                var actual = restored.Rooms[i];
                Assert.AreEqual(expected.InstanceId, actual.InstanceId, $"Room {i} instance ID");
                Assert.AreEqual(expected.Type.id, actual.Type.id, $"Room {i} type ID");
                Assert.AreEqual(expected.Origin, actual.Origin, $"Room {i} origin");
                Assert.AreEqual(expected.Size, actual.Size, $"Room {i} size");
            }

            Assert.AreEqual(expectedCells, Fingerprint(restored));
            Assert.IsTrue(restored.HasLobby);
            Assert.AreEqual(source.MinX, restored.MinX);
            Assert.AreEqual(source.MaxX, restored.MaxX);

            // Ordinary room, player scaffolding, stacked stairs, expanded elevator,
            // basement parking + ramp, and the sky lobby all survive the round trip.
            Assert.AreEqual(2, restored.Rooms.Count(r => r.Type.isStairs));
            Assert.AreEqual(28, restored.Rooms.Count(r => r.Type.isScaffolding));
            var shaft = restored.Rooms.Single(r => r.Type.isElevatorShaft);
            Assert.AreEqual(new Vector2Int(1, 4), shaft.Size);
            Assert.IsTrue(restored.IsElevatorColumnAt(new Vector2Int(14, 4)));
            var ramp = restored.Rooms.Single(r => r.Type.isParkingRamp);
            Assert.AreEqual(new Vector2Int(0, -1), ramp.Origin);
            Assert.IsTrue(restored.TryGetSkyLobbyOnFloor(15, out var skyLobby));
            Assert.AreEqual(new Vector2Int(2, 1), skyLobby.Size);
            CollectionAssert.AreEqual(new[] { 0, 15 }, restored.GetLobbyFloors());
        }

        [Test]
        public void Round_trip_preserves_room_runtime_state()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 0, 20, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(source.TryPlace(types.Office, new Vector2Int(0, 1), out var office));
            office.Evaluation = 64;
            office.Condition = 41;
            office.QueueCleanWork(25f);
            office.RecordConstructionSpend(4000, nowRealtime: 8f, isInitialPlace: true);

            var restored = new TowerGrid();
            var result = restored.RestoreRooms(
                source.CaptureRooms(nowRealtime: 12f),
                types.Registry(source),
                nowRealtime: 12f);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            var rebuilt = restored.Rooms.Single(r => r.InstanceId == office.InstanceId);
            Assert.AreEqual(64, rebuilt.Evaluation);
            Assert.AreEqual(41, rebuilt.Condition);
            Assert.IsTrue(rebuilt.Dirty);
            Assert.AreEqual(25f, rebuilt.CleanWorkRemaining);
            Assert.AreEqual(4000, rebuilt.ConstructionSpent);
            Assert.IsTrue(rebuilt.IsInBuildGrace(12f));
        }

        [Test]
        public void Room_behind_restored_stairs_reappears_when_the_stairs_are_demolished()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 0, 20, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(source.TryPlace(types.Office, new Vector2Int(0, 1), out var office));
            Assert.IsTrue(source.TryPlace(types.Stairs, new Vector2Int(0, 1), out var stairs));

            var restored = new TowerGrid();
            Assert.IsTrue(
                restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f).Success);

            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 1), out var visible));
            Assert.AreEqual(stairs.InstanceId, visible.InstanceId);

            Assert.IsTrue(restored.TryDemolishAt(new Vector2Int(0, 1), out var removed, out _, out var revealed));
            Assert.AreEqual(stairs.InstanceId, removed.InstanceId);
            Assert.AreEqual(1, revealed.Count);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 1), out var behind));
            Assert.AreEqual(office.InstanceId, behind.InstanceId);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(1, 1), out var behindRight));
            Assert.AreEqual(office.InstanceId, behindRight.InstanceId);
        }

        [Test]
        public void Room_and_lobby_behind_restored_elevator_reappear_when_it_is_demolished()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 0, 20, TowerGrid.LobbyFloor, out var lobby));
            Assert.IsTrue(source.TryPlace(types.Elevator, new Vector2Int(5, 0), out var shaft));
            Assert.IsTrue(source.TryPlace(types.Office, new Vector2Int(0, 1), out var office));

            var restored = new TowerGrid();
            Assert.IsTrue(
                restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f).Success);

            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(5, 0), out var overLobby));
            Assert.AreEqual(shaft.InstanceId, overLobby.InstanceId);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(5, 1), out var overOffice));
            Assert.AreEqual(shaft.InstanceId, overOffice.InstanceId);

            Assert.IsTrue(restored.TryDemolishAt(new Vector2Int(5, 1), out var removed, out _, out var revealed));
            Assert.AreEqual(shaft.InstanceId, removed.InstanceId);
            Assert.AreEqual(2, revealed.Count);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(5, 0), out var backLobby));
            Assert.AreEqual(lobby.InstanceId, backLobby.InstanceId);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(5, 1), out var backOffice));
            Assert.AreEqual(office.InstanceId, backOffice.InstanceId);
        }

        [Test]
        public void Sky_lobby_behind_a_restored_elevator_is_preserved()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 0, 10, TowerGrid.LobbyFloor, out _));
            BuildSupportBand(source, 0, 1, 14);
            Assert.IsTrue(source.TryPlaceSkyLobby(types.SkyLobby, 0, 1, 15, out var skyLobby));
            Assert.IsTrue(source.TryPlace(types.Elevator, new Vector2Int(0, 14), out var shaft));

            var restored = new TowerGrid();
            Assert.IsTrue(
                restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f).Success);

            Assert.IsTrue(restored.TryGetSkyLobbyOnFloor(15, out var rebuiltSky));
            Assert.AreEqual(skyLobby.InstanceId, rebuiltSky.InstanceId);
            Assert.IsTrue(restored.IsTransferLobbyFloor(15));
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(1, 15), out var exposedSky));
            Assert.AreEqual(skyLobby.InstanceId, exposedSky.InstanceId);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 15), out var covered));
            Assert.AreEqual(shaft.InstanceId, covered.InstanceId);

            Assert.IsTrue(restored.TryDemolishAt(new Vector2Int(0, 15), out _, out _, out _));
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 15), out var revealedSky));
            Assert.AreEqual(skyLobby.InstanceId, revealedSky.InstanceId);
        }

        [Test]
        public void Next_room_placed_after_restore_uses_the_highest_restored_id_plus_one()
        {
            var types = new TowerTypes();
            var source = BuildFullTower(types);
            var highestId = source.Rooms.Max(r => r.InstanceId);

            var restored = new TowerGrid();
            Assert.IsTrue(
                restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f).Success);

            Assert.IsTrue(restored.TryPlace(types.Office, new Vector2Int(10, 2), out var placed));
            Assert.AreEqual(highestId + 1, placed.InstanceId);
        }

        [Test]
        public void Restoring_into_a_populated_grid_replaces_it_entirely()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 4, 12, TowerGrid.LobbyFloor, out _));

            var destination = BuildDestination();
            Assert.IsTrue(
                destination.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f).Success);

            Assert.AreEqual(1, destination.Rooms.Count);
            Assert.AreEqual(4, destination.MinX);
            Assert.AreEqual(12, destination.MaxX);
            Assert.IsTrue(destination.HasLobby);
            Assert.IsFalse(destination.TryGetRoomAt(new Vector2Int(0, 1), out _));
            Assert.IsFalse(destination.TryGetRoomAt(new Vector2Int(15, 1), out _));
        }

        [TestCase("nullSnapshotList", GridRestoreError.NullSnapshots)]
        [TestCase("nullRegistry", GridRestoreError.NullRegistry)]
        [TestCase("nullRoomEntry", GridRestoreError.NullRoom)]
        [TestCase("nonPositiveInstanceId", GridRestoreError.InvalidInstanceId)]
        [TestCase("duplicateInstanceId", GridRestoreError.DuplicateInstanceId)]
        [TestCase("blankRoomTypeId", GridRestoreError.BlankRoomTypeId)]
        [TestCase("unknownRoomTypeId", GridRestoreError.UnknownRoomTypeId)]
        [TestCase("nonPositiveWidth", GridRestoreError.InvalidRoomSize)]
        [TestCase("nonPositiveHeight", GridRestoreError.InvalidRoomSize)]
        [TestCase("roomWidthMismatch", GridRestoreError.GeometryMismatch)]
        [TestCase("lobbyHeightMismatch", GridRestoreError.GeometryMismatch)]
        [TestCase("elevatorSpanTooShort", GridRestoreError.GeometryMismatch)]
        [TestCase("elevatorSpanTooTall", GridRestoreError.GeometryMismatch)]
        [TestCase("elevatorWidthMismatch", GridRestoreError.GeometryMismatch)]
        [TestCase("secondGroundLobby", GridRestoreError.MultipleGroundLobbies)]
        [TestCase("groundLobbyOffFloor", GridRestoreError.GroundLobbyFloorMismatch)]
        [TestCase("nonTransitOverlap", GridRestoreError.RoomOverlap)]
        [TestCase("stairsOverElevator", GridRestoreError.TransitOverlap)]
        [TestCase("rampOverElevator", GridRestoreError.TransitOverlap)]
        [TestCase("conflictingStairRoles", GridRestoreError.StairsRoleConflict)]
        [TestCase("instanceIdOverflow", GridRestoreError.InstanceIdOverflow)]
        public void Corrupt_snapshots_fail_and_leave_a_populated_grid_untouched(
            string corruption,
            GridRestoreError expected)
        {
            var types = new TowerTypes();
            var source = BuildCorruptionSource(types);
            var registry = types.Registry(source);
            var snapshots = source.CaptureRooms(nowRealtime: 3f);
            Corrupt(corruption, ref snapshots, ref registry);

            var destination = BuildDestination();
            var before = Fingerprint(destination);

            var result = destination.RestoreRooms(snapshots, registry, nowRealtime: 3f);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.ErrorCode);
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
            AssertDestinationIntact(destination, before);
        }

        [Test]
        public void Failed_room_state_restore_leaves_a_populated_grid_untouched()
        {
            var lobbyType = LobbyType();
            var officeType = OfficeType();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(lobbyType, 0, 20, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(source.TryPlace(officeType, new Vector2Int(0, 1), out _));
            var snapshots = source.CaptureRooms(0f);
            var registry = RoomTypeRegistry.CreateForTests(new[] { lobbyType, officeType });

            // The registry key stays "office" while the asset now reports a different ID, so
            // RoomInstance.RestoreSnapshot rejects the snapshot after the instance is built.
            officeType.id = "office_renamed";

            var destination = BuildDestination();
            var before = Fingerprint(destination);

            var result = destination.RestoreRooms(snapshots, registry, 0f);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GridRestoreError.RoomStateRestoreFailed, result.ErrorCode);
            AssertDestinationIntact(destination, before);
        }

        // A few bytes of malformed payload must not be able to ask for a billion-cell footprint.
        [TestCase("hugeLobbyWidth", GridRestoreError.RoomFootprintTooLarge)]
        [TestCase("hugeSkyLobbyWidth", GridRestoreError.RoomFootprintTooLarge)]
        [TestCase("hugeElevatorHeight", GridRestoreError.RoomFootprintTooLarge)]
        [TestCase("int32OverflowSquare", GridRestoreError.RoomFootprintTooLarge)]
        [TestCase("int32OverflowMaxWidth", GridRestoreError.RoomFootprintTooLarge)]
        [TestCase("aggregateOverBudget", GridRestoreError.RestoredFootprintTooLarge)]
        public void Oversized_snapshot_geometry_fails_fast_and_leaves_a_populated_grid_untouched(
            string oversized,
            GridRestoreError expected)
        {
            var types = new TowerTypes();
            var registry = types.Registry(new TowerGrid());
            var snapshots = OversizedSnapshots(oversized);

            var destination = BuildDestination();
            var before = Fingerprint(destination);

            var timer = Stopwatch.StartNew();
            var result = destination.RestoreRooms(snapshots, registry, 0f);
            timer.Stop();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.ErrorCode);
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
            Assert.Less(
                timer.Elapsed.TotalSeconds,
                2d,
                "Oversized geometry must be rejected before any footprint loop runs.");
            AssertDestinationIntact(destination, before);
        }

        [Test]
        public void Restore_budget_is_a_named_defensive_constant()
        {
            Assert.AreEqual(1_000_000, TowerGrid.MaxRestoredCells);
        }

        [Test]
        public void Legal_stacked_stairs_round_trip_while_placement_rejects_conflicting_runs()
        {
            var types = new TowerTypes();
            var source = new TowerGrid();
            Assert.IsTrue(source.TryPlaceLobby(types.Lobby, 0, 20, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(source.TryPlace(types.Office, new Vector2Int(0, 1), out var office));
            Assert.IsTrue(source.TryPlace(types.Stairs, new Vector2Int(0, 1), out var lower));
            Assert.IsTrue(source.TryPlace(types.Stairs, new Vector2Int(0, 2), out var upper));

            // The stacked pair above is legal; shifting the upper flight to (1, 2) puts its
            // bottom-left corner on the lower flight's top-right corner, which placement rejects.
            Assert.IsFalse(source.CanPlace(types.Stairs, new Vector2Int(1, 2)));

            var restored = new TowerGrid();
            var result = restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2, restored.Rooms.Count(r => r.Type.isStairs));
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 2), out var landing));
            Assert.AreEqual(upper.InstanceId, landing.InstanceId);

            // The lower flight still hides the office, and the upper flight still owns the landing.
            Assert.IsTrue(restored.TryDemolishAt(new Vector2Int(0, 1), out var removed, out _, out var revealed));
            Assert.AreEqual(lower.InstanceId, removed.InstanceId);
            Assert.AreEqual(1, revealed.Count);
            Assert.AreEqual(office.InstanceId, revealed[0].InstanceId);
            Assert.IsTrue(restored.TryGetRoomAt(new Vector2Int(0, 2), out var stillLanding));
            Assert.AreEqual(upper.InstanceId, stillLanding.InstanceId);
        }

        [Test]
        public void Stairs_over_ramp_over_rooms_demolish_in_stack_order_on_a_live_grid()
        {
            var types = new TowerTypes();
            var grid = BuildTransitStack(types, out var stack);

            AssertTransitStackDemolishesInOrder(grid, stack);
        }

        [Test]
        public void Stairs_over_ramp_over_rooms_demolish_in_stack_order_after_restore()
        {
            var types = new TowerTypes();
            var source = BuildTransitStack(types, out var stack);

            var restored = new TowerGrid();
            var result = restored.RestoreRooms(source.CaptureRooms(0f), types.Registry(source), 0f);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            AssertTransitStackDemolishesInOrder(restored, stack);
        }

        [Test]
        public void Restoring_many_stair_flights_stays_linear()
        {
            const int flightCount = 20_000;
            var types = new TowerTypes();
            var registry = types.Registry(new TowerGrid());
            var snapshots = new List<RoomSnapshotV1>(flightCount + 1)
            {
                Snapshot(1, "lobby", 0, TowerGrid.LobbyFloor, 601, 1)
            };

            // 2x2 flights on a 3-cell pitch never touch, so no pair can conflict and the
            // whole payload stays far inside the 1,000,000-cell budget.
            for (var i = 0; i < flightCount; i++)
                snapshots.Add(Snapshot(i + 2, "stairs", 3 * (i % 200), 1 + 3 * (i / 200), 2, 2));

            var grid = new TowerGrid();
            var timer = Stopwatch.StartNew();
            var result = grid.RestoreRooms(snapshots, registry, 0f);
            timer.Stop();

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(flightCount + 1, grid.Rooms.Count);
            // Comparing every flight against every other one would need billions of checks here.
            Assert.Less(
                timer.Elapsed.TotalSeconds,
                10d,
                "Stair-role validation must stay linear in the number of restored flights.");
        }

        [Test]
        public void Capture_returns_one_snapshot_per_room_in_grid_order()
        {
            var types = new TowerTypes();
            var source = BuildFullTower(types);

            var snapshots = source.CaptureRooms(nowRealtime: 5f);

            Assert.AreEqual(source.Rooms.Count, snapshots.Count);
            for (var i = 0; i < snapshots.Count; i++)
            {
                Assert.AreEqual(source.Rooms[i].InstanceId, snapshots[i].instanceId);
                Assert.AreEqual(source.Rooms[i].Type.id, snapshots[i].roomTypeId);
                Assert.AreEqual(source.Rooms[i].Origin.x, snapshots[i].originX);
                Assert.AreEqual(source.Rooms[i].Origin.y, snapshots[i].originY);
                Assert.AreEqual(source.Rooms[i].Size.x, snapshots[i].width);
                Assert.AreEqual(source.Rooms[i].Size.y, snapshots[i].height);
            }
        }

        [Test]
        public void Restoring_an_empty_snapshot_list_clears_the_grid()
        {
            var destination = BuildDestination();

            var result = destination.RestoreRooms(
                new List<RoomSnapshotV1>(),
                RoomTypeRegistry.CreateForTests(new[] { LobbyType() }),
                0f);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(0, destination.Rooms.Count);
            Assert.IsFalse(destination.HasLobby);
            Assert.IsFalse(destination.TryGetRoomAt(new Vector2Int(0, 1), out _));
        }

        static void Corrupt(
            string corruption,
            ref List<RoomSnapshotV1> snapshots,
            ref RoomTypeRegistry registry)
        {
            // Snapshot order: 0 lobby, 1 office, 2 stairs, 3 elevator, 4 parking ramp,
            // 5 stairs stacked legally on top of 2.
            switch (corruption)
            {
                case "nullSnapshotList": snapshots = null; break;
                case "nullRegistry": registry = null; break;
                case "nullRoomEntry": snapshots[1] = null; break;
                case "nonPositiveInstanceId": snapshots[1].instanceId = 0; break;
                case "duplicateInstanceId": snapshots[1].instanceId = snapshots[0].instanceId; break;
                case "blankRoomTypeId": snapshots[1].roomTypeId = "   "; break;
                case "unknownRoomTypeId": snapshots[1].roomTypeId = "office_of_the_future"; break;
                case "nonPositiveWidth": snapshots[1].width = 0; break;
                case "nonPositiveHeight": snapshots[1].height = -1; break;
                case "roomWidthMismatch": snapshots[1].width = 5; break;
                case "lobbyHeightMismatch": snapshots[0].height = 2; break;
                case "elevatorSpanTooShort": snapshots[3].height = 1; break;
                case "elevatorSpanTooTall":
                    snapshots[3].height = TowerGrid.MaxNormalElevatorSpan + 1;
                    break;
                case "elevatorWidthMismatch": snapshots[3].width = 2; break;
                case "secondGroundLobby":
                    snapshots.Add(Snapshot(900, "lobby", 40, TowerGrid.LobbyFloor, 3, 1));
                    break;
                case "groundLobbyOffFloor": snapshots[0].originY = 2; break;
                case "nonTransitOverlap": snapshots[1].originY = TowerGrid.LobbyFloor; break;
                case "stairsOverElevator": snapshots[2].originX = 14; break;
                case "rampOverElevator":
                    snapshots[4].originX = 14;
                    snapshots[4].originY = 1;
                    break;
                case "conflictingStairRoles": snapshots[5].originX = 1; break;
                case "instanceIdOverflow": snapshots[1].instanceId = int.MaxValue; break;
                default: Assert.Fail($"Unhandled corruption '{corruption}'."); break;
            }
        }

        static List<RoomSnapshotV1> OversizedSnapshots(string oversized)
        {
            const int overBudget = TowerGrid.MaxRestoredCells * 2;
            switch (oversized)
            {
                case "hugeLobbyWidth":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "lobby", 0, TowerGrid.LobbyFloor, overBudget, 1)
                    };
                case "hugeSkyLobbyWidth":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "lobby", 0, TowerGrid.LobbyFloor, 6, 1),
                        Snapshot(2, "sky_lobby", 0, 15, overBudget, 1)
                    };
                case "hugeElevatorHeight":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "lobby", 0, TowerGrid.LobbyFloor, 6, 1),
                        Snapshot(2, "elevator_normal", 2, 1, 1, overBudget)
                    };
                // 65_536 * 65_536 truncates to 0 in 32-bit arithmetic.
                case "int32OverflowSquare":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "office", 0, 1, 65_536, 65_536)
                    };
                // int.MaxValue * 2 truncates to -2 in 32-bit arithmetic.
                case "int32OverflowMaxWidth":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "lobby", 0, TowerGrid.LobbyFloor, int.MaxValue, 2)
                    };
                // Each sky lobby fits the budget on its own; together they blow past it.
                case "aggregateOverBudget":
                    return new List<RoomSnapshotV1>
                    {
                        Snapshot(1, "sky_lobby", 0, 15, 400_000, 1),
                        Snapshot(2, "sky_lobby", 0, 30, 400_000, 1),
                        Snapshot(3, "sky_lobby", 0, 45, 400_000, 1)
                    };
                default:
                    Assert.Fail($"Unhandled oversized case '{oversized}'.");
                    return null;
            }
        }

        static RoomSnapshotV1 Snapshot(
            int instanceId,
            string roomTypeId,
            int originX,
            int originY,
            int width,
            int height)
        {
            return new RoomSnapshotV1
            {
                instanceId = instanceId,
                roomTypeId = roomTypeId,
                originX = originX,
                originY = originY,
                width = width,
                height = height
            };
        }

        /// <summary>
        /// Basement parking and the ground lobby, both covered by a parking ramp, which is in turn
        /// covered by a flight of stairs. Every cell in the 2x2 stairs footprint therefore has two
        /// things underneath it.
        /// </summary>
        static TowerGrid BuildTransitStack(TowerTypes types, out TransitStack stack)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(types.Lobby, 0, 20, TowerGrid.LobbyFloor, out var lobby));
            Assert.IsTrue(grid.TryPlace(types.Parking, new Vector2Int(0, -1), out var parking));
            Assert.IsTrue(grid.TryPlace(types.Ramp, new Vector2Int(0, -1), out var ramp));
            Assert.IsTrue(grid.TryPlace(types.Stairs, new Vector2Int(0, -1), out var stairs));

            stack = new TransitStack
            {
                LobbyId = lobby.InstanceId,
                ParkingId = parking.InstanceId,
                RampId = ramp.InstanceId,
                StairsId = stairs.InstanceId
            };
            return grid;
        }

        static void AssertTransitStackDemolishesInOrder(TowerGrid grid, TransitStack stack)
        {
            var basementCell = new Vector2Int(0, -1);
            var lobbyCell = new Vector2Int(0, TowerGrid.LobbyFloor);

            AssertOwner(grid, basementCell, stack.StairsId);
            AssertOwner(grid, lobbyCell, stack.StairsId);

            Assert.IsTrue(grid.TryDemolishAt(basementCell, out var removedStairs, out var stairScaffolds, out _));
            Assert.AreEqual(stack.StairsId, removedStairs.InstanceId);
            CollectionAssert.IsEmpty(stairScaffolds);
            AssertOwner(grid, basementCell, stack.RampId);
            AssertOwner(grid, lobbyCell, stack.RampId);

            Assert.IsTrue(grid.TryDemolishAt(basementCell, out var removedRamp, out var rampScaffolds, out var revealed));
            Assert.AreEqual(stack.RampId, removedRamp.InstanceId);
            CollectionAssert.IsEmpty(rampScaffolds);
            AssertOwner(grid, basementCell, stack.ParkingId);
            AssertOwner(grid, lobbyCell, stack.LobbyId);
            CollectionAssert.AreEquivalent(
                new[] { stack.ParkingId, stack.LobbyId },
                revealed.Select(r => r.InstanceId).ToArray());

            // The uncovered remainder of each buried room was never disturbed.
            AssertOwner(grid, new Vector2Int(5, -1), stack.ParkingId);
            AssertOwner(grid, new Vector2Int(20, TowerGrid.LobbyFloor), stack.LobbyId);
        }

        static void AssertOwner(TowerGrid grid, Vector2Int cell, int expectedInstanceId)
        {
            Assert.IsTrue(grid.TryGetRoomAt(cell, out var owner), $"Nothing owns ({cell.x}, {cell.y}).");
            Assert.AreEqual(expectedInstanceId, owner.InstanceId, $"Owner of ({cell.x}, {cell.y})");
        }

        struct TransitStack
        {
            public int LobbyId;
            public int ParkingId;
            public int RampId;
            public int StairsId;
        }

        static void AssertDestinationIntact(TowerGrid destination, string before)
        {
            Assert.AreEqual(before, Fingerprint(destination), "Failed restore mutated the grid.");

            // The next instance ID must also be untouched.
            Assert.IsTrue(destination.TryPlaceScaffold(new Vector2Int(16, 1), out var probe));
            Assert.AreEqual(DestinationNextId, probe.InstanceId);

            // Under-stairs bookmarks must survive too.
            Assert.IsTrue(destination.TryDemolishAt(new Vector2Int(0, 1), out var removed, out _, out var revealed));
            Assert.IsTrue(removed.Type.isStairs);
            Assert.AreEqual(1, revealed.Count);
            Assert.IsTrue(destination.TryGetRoomAt(new Vector2Int(0, 1), out var office));
            Assert.AreEqual(DestinationOfficeId, office.InstanceId);
        }

        const int DestinationOfficeId = 2;
        const int DestinationNextId = 5;

        /// <summary>Lobby (1), office (2), stairs over the office (3), free scaffold (4).</summary>
        static TowerGrid BuildDestination()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 25, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(grid.TryPlace(OfficeType(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(StairsType(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(15, 1), out _));
            return grid;
        }

        static TowerGrid BuildCorruptionSource(TowerTypes types)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(types.Lobby, 0, 25, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(grid.TryPlace(types.Office, new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Stairs, new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Elevator, new Vector2Int(14, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Ramp, new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(types.Stairs, new Vector2Int(0, 2), out _));
            return grid;
        }

        static TowerGrid BuildFullTower(TowerTypes types)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(types.Lobby, 0, 29, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(grid.TryPlace(types.Office, new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Stairs, new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Stairs, new Vector2Int(0, 2), out _));
            Assert.IsTrue(grid.TryPlace(types.Elevator, new Vector2Int(14, 1), out var shaft));
            Assert.IsTrue(grid.TryExtendElevator(shaft, 1, 4, out _));
            Assert.IsTrue(grid.TryPlace(types.Office, new Vector2Int(10, 1), out _));
            Assert.IsTrue(grid.TryPlace(types.Parking, new Vector2Int(4, -1), out _));
            Assert.IsTrue(grid.TryPlace(types.Ramp, new Vector2Int(0, -1), out _));
            BuildSupportBand(grid, 20, 21, 14);
            Assert.IsTrue(grid.TryPlaceSkyLobby(types.SkyLobby, 20, 21, 15, out _));
            return grid;
        }

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        static string Fingerprint(TowerGrid grid)
        {
            var text = new StringBuilder();
            text.Append(grid.HasLobby).Append('|')
                .Append(grid.MinX).Append('|')
                .Append(grid.MaxX).Append('|');
            foreach (var room in grid.Rooms)
            {
                text.Append(room.InstanceId).Append(':')
                    .Append(room.Type == null ? "<null>" : room.Type.id).Append('@')
                    .Append(room.Origin.x).Append(',').Append(room.Origin.y).Append('#')
                    .Append(room.Size.x).Append('x').Append(room.Size.y).Append(';');
            }

            text.Append('|');
            for (var y = -6; y <= 20; y++)
            for (var x = -6; x <= 40; x++)
            {
                if (!grid.TryGetRoomAt(new Vector2Int(x, y), out var owner)) continue;
                text.Append(x).Append(',').Append(y).Append('=').Append(owner.InstanceId).Append(';');
            }

            return text.ToString();
        }

        sealed class TowerTypes
        {
            public RoomTypeSO Lobby { get; } = LobbyType();
            public RoomTypeSO SkyLobby { get; } = SkyLobbyType();
            public RoomTypeSO Office { get; } = OfficeType();
            public RoomTypeSO Stairs { get; } = StairsType();
            public RoomTypeSO Elevator { get; } = ElevatorType();
            public RoomTypeSO Parking { get; } = ParkingType();
            public RoomTypeSO Ramp { get; } = RampType();

            public RoomTypeRegistry Registry(TowerGrid source) =>
                RoomTypeRegistry.CreateForTests(
                    new[] { Lobby, SkyLobby, Office, Stairs, Elevator, Parking, Ramp, source.ScaffoldingType });
        }

        static RoomTypeSO LobbyType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "lobby";
            type.displayName = "Lobby";
            type.category = RoomCategory.Structure;
            type.size = Vector2Int.one;
            type.isLobby = true;
            type.allowAboveGround = true;
            return type;
        }

        static RoomTypeSO SkyLobbyType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "sky_lobby";
            type.displayName = "Sky Lobby";
            type.category = RoomCategory.Structure;
            type.size = Vector2Int.one;
            type.isSkyLobby = true;
            type.allowAboveGround = true;
            return type;
        }

        static RoomTypeSO OfficeType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "office";
            type.displayName = "Office";
            type.category = RoomCategory.Office;
            type.size = new Vector2Int(9, 1);
            type.allowAboveGround = true;
            return type;
        }

        static RoomTypeSO StairsType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "stairs";
            type.displayName = "Stairs";
            type.category = RoomCategory.Transit;
            type.size = new Vector2Int(2, 2);
            type.isStairs = true;
            type.allowAboveGround = true;
            type.allowBasement = true;
            return type;
        }

        static RoomTypeSO ElevatorType() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_normal",
                "Elevator",
                ElevatorShaftKind.Normal,
                requiredStars: 1,
                buildCost: 145000);

        static RoomTypeSO ParkingType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = ParkingStalls.ParkingId;
            type.displayName = "Parking";
            type.category = RoomCategory.Parking;
            type.size = new Vector2Int(6, 1);
            type.allowAboveGround = false;
            type.allowBasement = true;
            type.maxOccupants = 6;
            return type;
        }

        static RoomTypeSO RampType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = ParkingStalls.RampId;
            type.displayName = "Parking Ramp";
            type.category = RoomCategory.Transit;
            type.size = new Vector2Int(3, 2);
            type.isParkingRamp = true;
            type.allowAboveGround = false;
            type.allowBasement = true;
            return type;
        }
    }
}
