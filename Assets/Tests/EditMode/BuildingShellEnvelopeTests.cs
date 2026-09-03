using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingShellEnvelopeTests
    {
        [Test]
        public void OwnsStructurePaint_includes_scaffolding_and_lobby()
        {
            Assert.IsTrue(BuildingShellEnvelope.OwnsStructurePaint(Scaffold()));
            Assert.IsTrue(BuildingShellEnvelope.OwnsStructurePaint(Lobby()));
            Assert.IsFalse(BuildingShellEnvelope.OwnsStructurePaint(Shop()));
            Assert.IsFalse(BuildingShellEnvelope.OwnsStructurePaint(null));
        }

        [Test]
        public void ShouldSkipShellCell_skips_scaffolding_so_dirt_does_not_cover_it()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(3, -1), out _));

            Assert.IsTrue(BuildingShellEnvelope.ShouldSkipShellCell(new Vector2Int(3, -1), grid));
            Assert.IsFalse(BuildingShellEnvelope.ShouldSkipShellCell(new Vector2Int(4, -1), grid));
        }

        [Test]
        public void ComputeCells_fills_gap_between_basement_rooms()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            // Support B1 then place two B2 pads with a gap.
            for (var x = 0; x <= 20; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, -1), out _));

            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(0, -2), out _));
            Assert.IsTrue(grid.TryPlace(Pad(), new Vector2Int(10, -2), out _));

            var shell = BuildingShellEnvelope.ComputeCells(grid.Rooms);
            Assert.IsTrue(shell.Contains(new Vector2Int(5, -2)),
                "Shell fills the dirt gap between rooms — visual only, not walkable.");
            Assert.IsFalse(grid.TryGetRoomAt(new Vector2Int(5, -2), out _),
                "Gap cell must stay empty so pathfinding cannot walk dirt.");
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO Scaffold()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "scaffolding";
            so.isScaffolding = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Shop()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_food_fast";
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.size = Vector2Int.one;
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Pad()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "pad";
            so.category = RoomCategory.Commercial;
            so.size = Vector2Int.one;
            so.allowBasement = true;
            so.allowAboveGround = true;
            return so;
        }
    }
}
