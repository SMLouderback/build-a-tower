using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentSystemTests
    {
        [Test]
        public void Agent_rides_elevator_and_alights_at_destination()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);

            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);
            Assert.AreEqual(4, agent.ElevatorDestFloor);
            Assert.AreEqual(1, agent.TripLegIndex);

            elevators.Tick(0.1f);
            agents.Tick(0f, clock, grid);
            Assert.AreEqual(AgentPhase.Riding, agent.Phase);

            elevators.Tick(8.9f);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(AgentPhase.Working, agent.Phase);
            Assert.AreEqual(4, agent.Cell.y);
            Assert.IsFalse(elevators.Shafts[0].Car.PassengerIds.Contains(agent.Id));
        }

        [Test]
        public void Waiting_over_ten_game_minutes_increases_stress()
        {
            var grid = CreateFourFloorOfficeTower();
            var elevators = new ElevatorSystem();
            var router = new TransitRouter(new StairsPathfinder(), elevators);
            router.Rebuild(grid);
            var agents = new AgentSystem(router);
            agents.SyncHomes(grid);
            var agent = agents.Agents.Single(a => a.HomeRoom.Origin.y == 4);
            var clock = new GameClock(1f, agent.ArrivalMinute);
            agents.Tick(1f, clock, grid);
            Assert.AreEqual(AgentPhase.WaitingAtElevator, agent.Phase);

            clock.Tick(11f);
            agents.Tick(1f, clock, grid);

            Assert.Greater(agent.Stress, 0f);
            Assert.Greater(agent.ElevatorWaitMinutes, 10f);
        }

        static TowerGrid CreateFourFloorOfficeTower()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _));
            for (var floor = 1; floor <= 4; floor++)
                Assert.IsTrue(grid.TryPlace(Office(floor == 4), new Vector2Int(0, floor), out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out var elevator));
            Assert.IsTrue(grid.TryExtendElevator(elevator, 0, 4, out _));
            return grid;
        }

        static RoomTypeSO Lobby()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "lobby";
            room.isLobby = true;
            room.allowAboveGround = true;
            room.size = Vector2Int.one;
            return room;
        }

        static RoomTypeSO Office(bool occupied)
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "office";
            room.category = RoomCategory.Office;
            room.size = new Vector2Int(9, 1);
            room.maxOccupants = occupied ? 1 : 0;
            room.allowAboveGround = true;
            return room;
        }

        static RoomTypeSO Elevator()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "elevator";
            room.category = RoomCategory.Transit;
            room.size = new Vector2Int(1, 2);
            room.isElevatorShaft = true;
            room.allowAboveGround = true;
            room.allowBasement = true;
            return room;
        }
    }
}
