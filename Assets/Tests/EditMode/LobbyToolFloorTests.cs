using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class LobbyToolFloorTests
    {
        [Test]
        public void LobbyFloor_uses_ground_lobby_type()
        {
            var ground = ScriptableObject.CreateInstance<RoomTypeSO>();
            ground.isLobby = true;
            var sky = ScriptableObject.CreateInstance<RoomTypeSO>();
            sky.isSkyLobby = true;

            Assert.IsFalse(LobbyPlacement.UsesSkyLobby(TowerGrid.LobbyFloor));
            Assert.AreSame(ground, LobbyPlacement.ResolveType(TowerGrid.LobbyFloor, ground, sky));
        }

        [Test]
        public void Floor_above_lobby_uses_sky_lobby_type()
        {
            var ground = ScriptableObject.CreateInstance<RoomTypeSO>();
            ground.isLobby = true;
            var sky = ScriptableObject.CreateInstance<RoomTypeSO>();
            sky.isSkyLobby = true;

            Assert.IsTrue(LobbyPlacement.UsesSkyLobby(TowerGrid.LobbyFloor + 1));
            Assert.AreSame(sky, LobbyPlacement.ResolveType(15, ground, sky));
        }
    }
}
