using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AtriumStackRolesTests
    {
        [Test]
        public void Single_atrium_is_Solo()
        {
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var atrium));

            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, atrium));
        }

        [Test]
        public void Two_contiguous_stacked_atriums_lower_Mid_upper_Roof()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 6);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var lower));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 4), out var upper));

            Assert.AreEqual(AtriumStackRole.Mid, AtriumStackRoles.RoleFor(grid, lower));
            Assert.AreEqual(AtriumStackRole.Roof, AtriumStackRoles.RoleFor(grid, upper));
        }

        [Test]
        public void Y_gap_splits_into_two_Solo_stacks()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 10);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var low));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 5), out var high));

            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, low));
            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, high));
        }

        [Test]
        public void Three_contiguous_stacked_atriums_only_top_is_Roof()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 9);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var a));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 4), out var b));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 7), out var c));

            Assert.AreEqual(AtriumStackRole.Mid, AtriumStackRoles.RoleFor(grid, a));
            Assert.AreEqual(AtriumStackRole.Mid, AtriumStackRoles.RoleFor(grid, b));
            Assert.AreEqual(AtriumStackRole.Roof, AtriumStackRoles.RoleFor(grid, c));
        }

        [Test]
        public void Different_origin_x_are_separate_Solo_stacks()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 6);
            ScaffoldFootprint(grid, 8, 8, fromY: 1, toY: 6);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var west));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(8, 4), out var east));

            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, west));
            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, east));
        }

        [Test]
        public void Different_width_same_x_are_separate_Solo_stacks()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 6);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var wide));
            Assert.IsTrue(grid.TryPlace(NarrowAtriumSo(), new Vector2Int(0, 4), out var narrow));

            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, wide));
            Assert.AreEqual(AtriumStackRole.Solo, AtriumStackRoles.RoleFor(grid, narrow));
        }

        [Test]
        public void Leisure_atrium_id_counts_without_isAtrium_flag()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 6);
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: false), new Vector2Int(0, 1), out var lower));
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: true), new Vector2Int(0, 4), out var upper));

            Assert.AreEqual(AtriumStackRole.Mid, AtriumStackRoles.RoleFor(grid, lower));
            Assert.AreEqual(AtriumStackRole.Roof, AtriumStackRoles.RoleFor(grid, upper));
        }

        [Test]
        public void RolesByInstanceId_maps_all_atriums_in_stack()
        {
            var grid = GridWithLobby();
            ScaffoldFootprint(grid, 0, 8, fromY: 1, toY: 6);
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out var lower));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 4), out var upper));

            var roles = AtriumStackRoles.RolesByInstanceId(grid);

            Assert.AreEqual(AtriumStackRole.Mid, roles[lower.InstanceId]);
            Assert.AreEqual(AtriumStackRole.Roof, roles[upper.InstanceId]);
        }

        static TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _));
            return grid;
        }

        static void ScaffoldFootprint(TowerGrid grid, int originX, int width, int fromY, int toY)
        {
            for (var y = fromY; y <= toY; y++)
            for (var x = originX; x < originX + width; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _), $"scaffold {x},{y}");
        }

        static RoomTypeSO LobbySo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO AtriumSo(bool isAtrium = true)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_atrium";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            so.size = new Vector2Int(8, 3);
            so.allowAboveGround = true;
            so.isAtrium = isAtrium;
            return so;
        }

        static RoomTypeSO NarrowAtriumSo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_atrium";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            so.size = new Vector2Int(6, 3);
            so.allowAboveGround = true;
            so.isAtrium = true;
            return so;
        }
    }
}
