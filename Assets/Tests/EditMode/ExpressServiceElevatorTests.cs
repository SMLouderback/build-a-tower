using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ExpressServiceElevatorTests
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

        RoomTypeSO NormalElevator() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_normal", "Elevator", ElevatorShaftKind.Normal, requiredStars: 0);

        RoomTypeSO ExpressElevator() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_express", "Express", ElevatorShaftKind.Express, requiredStars: 3);

        RoomTypeSO ServiceElevator() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_service", "Service", ElevatorShaftKind.Service, requiredStars: 4);

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        [Test]
        public void CanPlace_express_requires_two_cell_width()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 5);

            var express = ExpressElevator();
            Assert.IsTrue(grid.CanPlace(express, new Vector2Int(2, 1)));

            var invalidExpress = ExpressElevator();
            invalidExpress.size = new Vector2Int(1, 2);
            Assert.IsFalse(grid.CanPlace(invalidExpress, new Vector2Int(2, 1)));
        }

        [Test]
        public void Express_shaft_serves_only_lobby_floors_within_span()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 45);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 30, out _);

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 1), out var shaft));
            Assert.IsTrue(grid.TryExtendElevator(shaft, 1, 45, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            var express = elevators.Shafts.Single(s => s.Kind == ElevatorShaftKind.Express);

            Assert.IsTrue(express.Serves(0));
            Assert.IsTrue(express.Serves(30));
            Assert.IsFalse(express.Serves(15));
            Assert.IsFalse(express.Serves(45));
        }

        [Test]
        public void TryPlanTrip_uses_express_between_ground_and_sky_lobby()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 45);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 30, out _);

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 1), out var expressRoom));
            Assert.IsTrue(grid.TryExtendElevator(expressRoom, 1, 45, out _));

            var stairs = new StairsPathfinder();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(stairs, elevators);
            router.Rebuild(grid);

            Assert.IsTrue(router.TryPlanTrip(new Vector2Int(4, 0), new Vector2Int(4, 30), out var legs));
            Assert.IsTrue(legs.Any(l =>
                l.Kind == TransitLegKind.Elevator &&
                l.EntryFloor == 0 &&
                l.ExitFloor == 30));
        }

        [Test]
        public void TryPlanTrip_prefers_service_shaft_for_maid_when_walk_is_equal()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 12, 0, out _);
            BuildSupportBand(grid, 0, 12, 12);

            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(3, 1), out var normalRoom));
            Assert.IsTrue(grid.TryExtendElevator(normalRoom, 1, 12, out _));
            Assert.IsTrue(grid.TryPlace(ServiceElevator(), new Vector2Int(7, 1), out var serviceRoom));
            Assert.IsTrue(grid.TryExtendElevator(serviceRoom, 1, 12, out _));

            var stairs = new StairsPathfinder();
            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            var serviceShaft = elevators.Shafts.Single(s => s.Kind == ElevatorShaftKind.Service);

            var router = new TransitRouter(stairs, elevators);
            router.Rebuild(grid);

            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(5, 2),
                new Vector2Int(5, 10),
                agentStress: 0f,
                AgentRole.Maid,
                out var legs));

            var elevatorLeg = legs.First(l => l.Kind == TransitLegKind.Elevator);
            Assert.AreEqual(serviceShaft.X, elevatorLeg.ElevatorX);
        }
        [Test]
        public void TryExtendElevator_express_reaches_dragged_floor_not_half()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 20);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 15, out _);

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 1), out var shaft));
            Assert.IsTrue(grid.TryExtendElevator(shaft, 1, 20, out _));

            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(2, 20), out var atTop));
            Assert.IsTrue(atTop.Type.isElevatorShaft);
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(3, 20), out var atTopRight));
            Assert.AreEqual(atTop.InstanceId, atTopRight.InstanceId);
            Assert.AreEqual(20, atTop.Origin.y + atTop.Size.y - 1);
        }

        [Test]
        public void Service_elevator_can_extend_into_basement()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            for (var y = -5; y <= 5; y++)
            for (var x = 0; x <= 10; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));

            var service = ServiceElevator();
            Assert.IsTrue(service.allowBasement);
            Assert.IsTrue(grid.TryPlace(service, new Vector2Int(4, -4), out var shaft));
            Assert.IsTrue(grid.TryExtendElevator(shaft, -5, -1, out _));
            Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(4, -5), out var updated));
            Assert.AreEqual(-5, updated.Origin.y);
            Assert.AreEqual(-1, updated.Origin.y + updated.Size.y - 1);
        }

        [Test]
        public void Express_elevator_allows_span_up_to_100_floors()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 100);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 30, out _);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 60, out _);
            grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 90, out _);

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 1), out var shaft));
            Assert.IsTrue(grid.CanExtendElevator(shaft, 1, 100));
            Assert.IsFalse(grid.CanExtendElevator(shaft, 1, 101));
        }

        [Test]
        public void Normal_elevator_limited_to_32_floors()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 40);

            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(2, 1), out var shaft));
            Assert.IsTrue(grid.CanExtendElevator(shaft, 1, 32));
            Assert.IsFalse(grid.CanExtendElevator(shaft, 1, 33));
        }

        [Test]
        public void Working_agent_on_elevator_column_is_hidden_from_view()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 5);
            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(3, 1), out _));

            var agent = new Agent(1, AgentRole.OfficeWorker, null, new Vector2Int(3, 2))
            {
                Phase = AgentPhase.Working,
                Visible = true
            };
            Assert.IsTrue(AgentView.IsHiddenBehindElevatorShaft(agent, grid));
            agent.Phase = AgentPhase.WaitingAtElevator;
            Assert.IsFalse(AgentView.IsHiddenBehindElevatorShaft(agent, grid));
        }
    }
}
