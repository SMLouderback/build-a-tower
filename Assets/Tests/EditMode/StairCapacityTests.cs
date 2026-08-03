using System.Collections.Generic;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class StairCapacityTests
    {
        static readonly FieldInfo AgentsField =
            typeof(AgentSystem).GetField("_agents", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void StepMovement_blocks_second_agent_at_stair_cap_and_adds_stress()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out var stairs));

            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            var agents = new AgentSystem(router, stairCapacity: new StairCapacity(1));

            var approach = new Vector2Int(9, 0);
            var stairCell = new Vector2Int(10, 0);
            Assert.IsTrue(grid.TryGetRoomAt(stairCell, out var atStairs));
            Assert.IsTrue(atStairs.Type.isStairs);
            Assert.AreEqual(stairs.InstanceId, atStairs.InstanceId);

            // StreetVisitor has no schedule updater — keeps Forced Moving path intact.
            // PhaseAfterMove=Working avoids VisitingShop dwell cleanup mid-Tick.
            var first = new Agent(101, AgentRole.StreetVisitor, null, approach)
            {
                Phase = AgentPhase.Moving,
                Stress = 0f,
                Path = new List<Vector2Int> { stairCell },
                PathIndex = 0,
                GoalCell = stairCell,
                PhaseAfterMove = AgentPhase.Working
            };
            var second = new Agent(102, AgentRole.StreetVisitor, null, approach)
            {
                Phase = AgentPhase.Moving,
                Stress = 0f,
                Path = new List<Vector2Int> { stairCell },
                PathIndex = 0,
                GoalCell = stairCell,
                PhaseAfterMove = AgentPhase.Working
            };

            var list = (List<Agent>)AgentsField.GetValue(agents);
            list.Add(first);
            list.Add(second);

            var clock = new GameClock(1f, 8 * 60);
            agents.Tick(1f, clock, grid);

            Assert.AreEqual(stairCell, first.Cell);
            Assert.AreEqual(stairs.InstanceId, first.StairsOccupancyRoomId);
            Assert.AreEqual(approach, second.Cell);
            Assert.AreEqual(0, second.StairsOccupancyRoomId);
            Assert.Greater(
                second.Stress,
                0f,
                "Blocked waiter should gain StairWaitStressPerMinute stress");
            Assert.AreEqual(
                AgentSystem.StairWaitStressPerMinute,
                second.Stress,
                0.0001f);
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
            so.category = RoomCategory.Transit;
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        [Test]
        public void TryEnter_five_agents_ok_sixth_rejected()
        {
            var capacity = new StairCapacity();
            const int roomId = 10;

            for (var agentId = 1; agentId <= 5; agentId++)
                Assert.IsTrue(capacity.TryEnter(roomId, agentId));

            Assert.IsFalse(capacity.TryEnter(roomId, 6));
            Assert.AreEqual(5, capacity.Occupancy(roomId));
        }

        [Test]
        public void Leave_one_then_sixth_can_enter()
        {
            var capacity = new StairCapacity();
            const int roomId = 10;

            for (var agentId = 1; agentId <= 5; agentId++)
                Assert.IsTrue(capacity.TryEnter(roomId, agentId));

            capacity.Leave(roomId, 3);
            Assert.AreEqual(4, capacity.Occupancy(roomId));
            Assert.IsTrue(capacity.TryEnter(roomId, 6));
            Assert.AreEqual(5, capacity.Occupancy(roomId));
        }

        [Test]
        public void TryEnter_same_agent_twice_stays_occupancy_one()
        {
            var capacity = new StairCapacity();
            const int roomId = 10;

            Assert.IsTrue(capacity.TryEnter(roomId, 42));
            Assert.IsTrue(capacity.TryEnter(roomId, 42));
            Assert.AreEqual(1, capacity.Occupancy(roomId));
        }

        [Test]
        public void Occupancy_independent_per_stairs_room_id()
        {
            var capacity = new StairCapacity();

            for (var agentId = 1; agentId <= 3; agentId++)
                Assert.IsTrue(capacity.TryEnter(100, agentId));

            for (var agentId = 10; agentId <= 12; agentId++)
                Assert.IsTrue(capacity.TryEnter(200, agentId));

            Assert.AreEqual(3, capacity.Occupancy(100));
            Assert.AreEqual(3, capacity.Occupancy(200));
            Assert.AreEqual(0, capacity.Occupancy(999));
        }

        [Test]
        public void DefaultCap_is_five()
        {
            var capacity = new StairCapacity();
            Assert.AreEqual(StairCapacity.DefaultCap, capacity.Cap);
            Assert.AreEqual(5, StairCapacity.DefaultCap);
        }

        [Test]
        public void Leave_missing_agent_is_safe()
        {
            var capacity = new StairCapacity();
            capacity.Leave(1, 99);
            Assert.AreEqual(0, capacity.Occupancy(1));
        }

        [Test]
        public void Clear_wipes_all_occupancy()
        {
            var capacity = new StairCapacity();
            capacity.TryEnter(1, 1);
            capacity.TryEnter(2, 2);
            capacity.Clear();
            Assert.AreEqual(0, capacity.Occupancy(1));
            Assert.AreEqual(0, capacity.Occupancy(2));
        }
    }
}
