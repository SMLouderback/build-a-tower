using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ScaffoldPlacementTests
    {
        [Test]
        public void CanPlaceScaffold_empty_supported_cell_succeeds()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            Assert.IsTrue(grid.CanPlaceScaffold(new Vector2Int(0, 1)));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(0, 1), out var stud));
            Assert.IsTrue(stud.Type.isScaffolding);
            Assert.AreEqual(TowerGrid.ScaffoldBuildCost, stud.Type.buildCost);
            Assert.AreEqual(750, TowerGrid.ScaffoldBuildCost);
        }

        [Test]
        public void CanPlaceScaffold_rejects_lobby_floor_and_unsupported()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            Assert.IsFalse(grid.CanPlaceScaffold(new Vector2Int(0, 0)));
            Assert.IsFalse(grid.CanPlaceScaffold(new Vector2Int(0, 2)));
        }

        [Test]
        public void CanPlaceScaffold_rejects_occupied_cell()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsFalse(grid.CanPlaceScaffold(new Vector2Int(0, 1)));
        }

        [Test]
        public void Scaffold_supports_room_above_with_gap_beside()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsFalse(grid.CanPlace(Office(), new Vector2Int(10, 2)));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(10, 1), out _));
            Assert.IsTrue(grid.CanPlace(Office(), new Vector2Int(10, 2)));
        }

        [Test]
        public void Pathfinder_walks_across_scaffolding_between_rooms()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(OfficeNarrow(), new Vector2Int(0, 1), out var left));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(3, 1), out _));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(4, 1), out _));
            Assert.IsTrue(grid.TryPlace(OfficeNarrow(), new Vector2Int(5, 1), out var right));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));

            var pf = new StairsPathfinder();
            pf.Rebuild(grid);
            Assert.IsTrue(pf.TryFindPath(left.Origin, right.Origin, maxFloorSpan: -1, out var path));
            Assert.GreaterOrEqual(path.Count, 3);
        }

        [Test]
        public void Load_bearing_player_scaffold_cannot_be_demolished()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 2), out _));
            Assert.IsFalse(grid.TryDemolishAt(new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var still));
            Assert.IsTrue(still.Type.isScaffolding);
        }

        [Test]
        public void Non_load_bearing_scaffold_can_be_demolished()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(0, 1), out var removed));
            Assert.IsTrue(removed.Type.isScaffolding);
            Assert.IsFalse(grid.TryGetRoomAt(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void SpendAndPlace_scaffold_debits_wallet()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            var wallet = new FundsWallet(2_000);
            Assert.IsTrue(grid.CanPlaceScaffold(new Vector2Int(0, 1)));
            Assert.IsTrue(wallet.TrySpend(TowerGrid.ScaffoldBuildCost));
            Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(0, 1), out _));
            Assert.AreEqual(2_000 - 750, wallet.Balance);
        }

        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.displayName = "Lobby";
            so.category = RoomCategory.Structure;
            so.size = Vector2Int.one;
            so.buildCost = 1000;
            so.isLobby = true;
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.displayName = "Office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.buildCost = 40000;
            so.allowAboveGround = true;
            so.allowBasement = false;
            return so;
        }

        RoomTypeSO OfficeNarrow()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_narrow";
            so.displayName = "Office Narrow";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(3, 1);
            so.allowAboveGround = true;
            so.maxOccupants = 1;
            return so;
        }

        RoomTypeSO Stairs()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.category = RoomCategory.Transit;
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }
    }
}
