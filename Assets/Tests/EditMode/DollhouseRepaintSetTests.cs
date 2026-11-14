using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class DollhouseRepaintSetTests
    {
        [Test]
        public void Collect_returns_unique_rooms_sharing_any_edge_with_changed_footprint()
        {
            var grid = GridWithLobby();
            var changed = PlaceMicroOffice(grid, new Vector2Int(10, 1));
            var left = PlaceMicroOffice(grid, new Vector2Int(9, 1));
            var right = PlaceMicroOffice(grid, new Vector2Int(11, 1));
            var top = PlaceMicroOffice(grid, new Vector2Int(10, 2));

            var rooms = DollhouseRepaintSet.Collect(grid, changed);

            CollectionAssert.AreEquivalent(
                new[] { left.InstanceId, right.InstanceId, top.InstanceId },
                RoomIds(rooms));
        }

        [Test]
        public void Collect_works_after_changed_room_was_removed_from_grid()
        {
            var grid = GridWithLobby();
            var changed = PlaceMicroOffice(grid, new Vector2Int(10, 1));
            var left = PlaceMicroOffice(grid, new Vector2Int(9, 1));
            var right = PlaceMicroOffice(grid, new Vector2Int(11, 1));
            Assert.IsTrue(grid.TryDemolishAt(changed.Origin, out var removed));

            var rooms = DollhouseRepaintSet.Collect(grid, removed);

            CollectionAssert.AreEquivalent(
                new[] { left.InstanceId, right.InstanceId },
                RoomIds(rooms));
        }

        [Test]
        public void Collect_ignores_scaffolding_neighbors()
        {
            var grid = GridWithLobby();
            var changed = PlaceMicroOffice(grid, new Vector2Int(10, 1));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(9, 1), out _));

            var rooms = DollhouseRepaintSet.Collect(grid, changed);

            Assert.IsEmpty(rooms);
        }

        static int[] RoomIds(System.Collections.Generic.IReadOnlyList<RoomInstance> rooms)
        {
            var ids = new int[rooms.Count];
            for (var i = 0; i < rooms.Count; i++)
                ids[i] = rooms[i].InstanceId;
            return ids;
        }

        static TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
        }

        static RoomInstance PlaceMicroOffice(TowerGrid grid, Vector2Int origin)
        {
            var type = MicroOffice();
            EnsureFootprintSupport(grid, origin, type.size);
            Assert.IsTrue(grid.TryPlace(type, origin, out var room));
            return room;
        }

        static void EnsureFootprintSupport(TowerGrid grid, Vector2Int origin, Vector2Int size)
        {
            for (var dx = 0; dx < size.x; dx++)
            for (var dy = 0; dy < size.y; dy++)
            {
                var y = origin.y + dy;
                if (y <= TowerGrid.LobbyFloor)
                    continue;
                var below = new Vector2Int(origin.x + dx, y - 1);
                if (grid.TryGetRoomAt(below, out _))
                    continue;
                Assert.IsTrue(grid.TryPlace(MicroOffice(), below, out _));
            }
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO MicroOffice()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_micro";
            so.category = RoomCategory.Office;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

    }
}
