using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingShellEnvelopeTests
    {
        [Test]
        public void Empty_grid_has_no_shell_cells()
        {
            var grid = new TowerGrid();
            Assert.AreEqual(0, BuildingShellEnvelope.ComputeCells(grid.Rooms).Count);
        }

        [Test]
        public void Single_floor_row_fills_horizontal_span()
        {
            var grid = PlaceLobbyAndRoom(Office(), new Vector2Int(2, 1), new Vector2Int(6, 1));
            var shell = BuildingShellEnvelope.ComputeCells(grid.Rooms);

            Assert.IsTrue(shell.Contains(new Vector2Int(2, 1)));
            Assert.IsTrue(shell.Contains(new Vector2Int(6, 1)));
            Assert.IsTrue(shell.Contains(new Vector2Int(4, 1)));
            Assert.IsFalse(shell.Contains(new Vector2Int(1, 1)));
        }

        [Test]
        public void Upper_overhang_extends_shell_on_lower_floor()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, TowerGrid.LobbyFloor, out _);
            grid.TryPlace(Office(4), new Vector2Int(0, 1), out _);
            grid.TryPlace(Office(8), new Vector2Int(0, 2), out _);

            var shell = BuildingShellEnvelope.ComputeCells(grid.Rooms);

            // Floor 1 only had width 4, but floor 2 spans 8 — shell should fill the notch.
            Assert.IsTrue(shell.Contains(new Vector2Int(7, 1)));
        }

        [Test]
        public void Skips_lobby_and_scaffolding_cells()
        {
            var grid = PlaceLobbyAndRoom(Office(), new Vector2Int(0, 1), new Vector2Int(3, 1));
            grid.TryGetRoomAt(new Vector2Int(0, TowerGrid.LobbyFloor), out var lobby);

            Assert.IsTrue(BuildingShellEnvelope.ShouldSkipShellCell(
                new Vector2Int(0, TowerGrid.LobbyFloor),
                grid));
            Assert.IsFalse(BuildingShellEnvelope.ShouldSkipShellCell(new Vector2Int(1, 1), grid));
            Assert.IsNotNull(lobby);
        }

        static TowerGrid PlaceLobbyAndRoom(
            RoomTypeSO type,
            Vector2Int origin,
            Vector2Int farCell)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, TowerGrid.LobbyFloor, out _);
            grid.TryPlace(type, origin, out _);
            Assert.IsTrue(grid.TryGetRoomAt(farCell, out _));
            return grid;
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.isLobby = true;
            return so;
        }

        static RoomTypeSO Office(int width = 3)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_test";
            so.size = new Vector2Int(width, 1);
            so.allowAboveGround = true;
            return so;
        }
    }
}
