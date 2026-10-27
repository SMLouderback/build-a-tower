using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SkyLobbyRoutingTests
    {
        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO SkyLobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "sky_lobby";
            so.isSkyLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO Elevator()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "elevator_normal";
            so.isElevatorShaft = true;
            so.size = new Vector2Int(1, 2);
            so.allowAboveGround = true;
            return so;
        }

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        [Test]
        public void TryPlanTrip_uses_sky_lobby_transfer_between_stacked_shafts()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 45);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 30, out _));

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(2, 1), out var lowerShaft));
            Assert.IsTrue(grid.TryExtendElevator(lowerShaft, 1, 30, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(6, 30), out var upperShaft));
            Assert.IsTrue(grid.TryExtendElevator(upperShaft, 30, 45, out _));

            var stairs = new StairsPathfinder();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(stairs, elevators);
            router.Rebuild(grid);

            var start = new Vector2Int(4, 45);
            var goal = new Vector2Int(4, 5);
            Assert.IsTrue(router.TryPlanTrip(start, goal, out var legs));
            Assert.GreaterOrEqual(legs.Count(l => l.Kind == TransitLegKind.Elevator), 2);
        }
    }
}
