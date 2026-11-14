using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TransferFloorProviderTests
    {
        [Test]
        public void TransferFloorsBetween_returns_lobbies_in_vertical_range()
        {
            var grid = new TowerGrid();
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.isLobby = true;
            grid.TryPlaceLobby(lobby, 0, 5, 0, out _);

            for (var y = 1; y <= 44; y++)
            for (var x = 0; x <= 5; x++)
                grid.TryPlaceScaffold(new Vector2Int(x, y), out _);

            var sky = ScriptableObject.CreateInstance<RoomTypeSO>();
            sky.isSkyLobby = true;
            grid.TryPlaceSkyLobby(sky, 0, 5, 30, out _);

            var between = new List<int>(TransferFloorProvider.TransferFloorsBetween(45, 5, grid));
            CollectionAssert.AreEqual(new[] { 0, 30 }, between);
        }
    }
}
