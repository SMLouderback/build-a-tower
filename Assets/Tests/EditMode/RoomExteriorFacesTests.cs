using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomExteriorFacesTests
    {
        [Test]
        public void Isolated_room_has_both_sides_exterior()
        {
            var grid = GridWithLobbyAndStairs();
            var office = PlaceOffice(grid, new Vector2Int(10, 2));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out var right);

            Assert.IsTrue(left);
            Assert.IsTrue(right);
        }

        [Test]
        public void Neighbor_on_left_makes_left_interior_right_stays_exterior()
        {
            var grid = GridWithLobbyAndStairs();
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(9, 1), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(9, 2), out _));
            var office = PlaceOffice(grid, new Vector2Int(10, 2));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out var right);

            Assert.IsFalse(left);
            Assert.IsTrue(right);
        }

        [Test]
        public void Neighbor_on_right_makes_right_interior()
        {
            var grid = GridWithLobbyAndStairs();
            var office = PlaceOffice(grid, new Vector2Int(10, 2));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(19, 1), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(19, 2), out _));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out var right);

            Assert.IsTrue(left);
            Assert.IsFalse(right);
        }

        [Test]
        public void Stairs_beside_room_counts_as_occupied_on_that_side()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            var office = PlaceOffice(grid, new Vector2Int(2, 1));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out _);

            Assert.IsFalse(left, "Stairs at (1,1) touch the office left edge at x=2.");
        }

        [Test]
        public void Shell_filler_gap_does_not_make_side_interior()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            for (var x = 0; x <= 20; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, -1), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, -2), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(10, -2), out var eastPad));

            Assert.IsFalse(grid.TryGetRoomAt(new Vector2Int(9, -2), out _),
                "Gap between basement pads is shell-only, not a room cell.");

            RoomExteriorFaces.Evaluate(grid, eastPad, out var left, out _);

            Assert.IsTrue(left);
        }

        [Test]
        public void Room_on_floor_below_does_not_affect_horizontal_exterior()
        {
            var grid = GridWithLobbyAndStairs();
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(4, 1), out _));
            var office = PlaceOffice(grid, new Vector2Int(5, 2));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out _);

            Assert.IsTrue(left, "Neighbor at (4,1) is not immediately left of the office left edge at y=2.");
        }

        [Test]
        public void Multi_floor_room_left_blocked_if_any_edge_row_has_neighbor()
        {
            var grid = GridWithLobbyAndStairs();
            var dept = DepartmentStore();
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(1, 1), out _));
            Assert.IsTrue(grid.TryPlace(dept, new Vector2Int(2, 1), out var shop));

            RoomExteriorFaces.Evaluate(grid, shop, out var left, out _);

            Assert.IsFalse(left);
        }

        [Test]
        public void Scaffolding_beside_does_not_count_as_room_neighbor()
        {
            var grid = GridWithLobbyAndStairs();
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(9, 1), out _));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(9, 2), out _));
            var office = PlaceOffice(grid, new Vector2Int(10, 2));

            RoomExteriorFaces.Evaluate(grid, office, out var left, out _);

            Assert.IsTrue(left);
        }

        static TowerGrid GridWithLobbyAndStairs()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(0, 0), out _));
            return grid;
        }

        static RoomInstance PlaceOffice(TowerGrid grid, Vector2Int origin)
        {
            var type = Office();
            EnsureFootprintSupport(grid, origin, type.size);
            Assert.IsTrue(grid.TryPlace(type, origin, out var office));
            return office;
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
                Assert.IsTrue(grid.TryPlace(Pad(), below, out _));
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

        static RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_mid_standard";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Pad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "pad";
            so.category = RoomCategory.Office;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO DepartmentStore()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_retail_department";
            so.category = RoomCategory.Commercial;
            so.size = new Vector2Int(8, 2);
            so.allowAboveGround = true;
            return so;
        }
    }
}
