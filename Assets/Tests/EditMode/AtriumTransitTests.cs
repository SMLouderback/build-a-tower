using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AtriumTransitTests
    {
        // Layout: lobby G (0). Atrium 8x3 at (0,1) -> x 0..7, y 1..3.
        // Stairs A (8,3) covers y 3..4; stairs B (10,4) covers y 4..5; floor 4 bridges them.
        static TowerGrid BuildGrid()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _));
            for (var y = 1; y <= 2; y++)
            for (var x = 8; x <= 11; x++)
                Assert.IsTrue(grid.TryPlace(PadSo(), new Vector2Int(x, y), out _), $"pad {x},{y}");
            Assert.IsTrue(grid.TryPlace(PadSo(), new Vector2Int(10, 3), out _), "pad 10,3");
            Assert.IsTrue(grid.TryPlace(PadSo(), new Vector2Int(11, 3), out _), "pad 11,3");
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out _), "atrium");
            Assert.IsTrue(grid.TryPlace(StairsSo(), new Vector2Int(8, 3), out _), "stairs A");
            Assert.IsTrue(grid.TryPlace(StairsSo(), new Vector2Int(10, 4), out _), "stairs B");
            return grid;
        }

        [Test]
        public void Path_atrium_bottom_to_top_without_external_stairs()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out _));

            var pf = new StairsPathfinder();
            pf.Rebuild(grid);

            Assert.IsTrue(pf.TryFindPath(new Vector2Int(3, 1), new Vector2Int(3, 3), out var path));
            Assert.AreEqual(3, path.Count);
            Assert.AreEqual(new Vector2Int(3, 2), path[1]);
        }

        [Test]
        public void Atrium_does_not_link_to_non_atrium_cells_vertically()
        {
            var grid = BuildGrid();
            var pf = new StairsPathfinder();
            pf.Rebuild(grid);

            // Pads at x=9 are not stairs/atrium: no direct vertical edge. The only route
            // between them detours through the atrium shaft (x<=7).
            Assert.IsTrue(pf.TryFindPath(new Vector2Int(9, 1), new Vector2Int(9, 2), -1, out var path));
            Assert.Greater(path.Count, 2);
            Assert.IsTrue(pf.IsAtriumCell(path[path.Count / 2]) || path.Exists(c => pf.IsAtriumCell(c)));
        }

        [Test]
        public void Atrium_floors_are_transfer_floors()
        {
            var grid = BuildGrid();

            Assert.IsTrue(grid.IsTransferLobbyFloor(0));
            Assert.IsTrue(grid.IsTransferLobbyFloor(1));
            Assert.IsTrue(grid.IsTransferLobbyFloor(2));
            Assert.IsTrue(grid.IsTransferLobbyFloor(3));
            Assert.IsFalse(grid.IsTransferLobbyFloor(4));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, grid.GetTransferFloors());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, TransferFloorProvider.GetSortedTransferFloors(grid));
        }

        [Test]
        public void Atrium_floors_are_not_lobby_floors()
        {
            var grid = BuildGrid();

            // Lobby floors drive sky-lobby spacing and Express stops; atrium is neither.
            CollectionAssert.AreEqual(new[] { 0 }, grid.GetLobbyFloors());
            Assert.IsTrue(grid.IsLobbyFloor(0));
            Assert.IsFalse(grid.IsLobbyFloor(2));
            Assert.IsTrue(grid.IsTransferLobbyFloor(2));
        }

        [Test]
        public void Atrium_does_not_block_sky_lobby_spacing()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 10, 0, out _));
            Assert.IsTrue(grid.TryPlace(AtriumSo(), new Vector2Int(0, 1), out _));
            for (var y = 4; y <= 14; y++)
            for (var x = 0; x <= 5; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _), $"scaffold {x},{y}");

            // Atrium tops out at floor 3; floor 15 is 12 away but only the ground lobby counts.
            Assert.IsTrue(grid.CanPlaceSkyLobby(0, 5, 15));
        }

        [Test]
        public void Atrium_internal_crossing_adds_no_stress_or_span()
        {
            var grid = BuildGrid();
            var agent = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
            agent.Stress = 0f;

            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(7, 1), new Vector2Int(7, 2), out var refused));
            Assert.IsFalse(refused);
            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(7, 2), new Vector2Int(7, 3), out refused));
            Assert.IsFalse(refused);

            Assert.AreEqual(0f, agent.Stress);
            Assert.AreEqual(0, agent.StairsFloorsCrossedThisLeg);
        }

        [Test]
        public void External_stairs_from_atrium_top_restart_comfort_chain()
        {
            var grid = BuildGrid();
            var agent = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
            agent.Stress = 0f;
            // Already climbed 3 floors before reaching an atrium floor.
            agent.StairsFloorsCrossedThisLeg = 3;
            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(0, 2), new Vector2Int(0, 3), out _));
            Assert.AreEqual(0, agent.StairsFloorsCrossedThisLeg);

            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(8, 3), new Vector2Int(8, 4), out _));
            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(10, 4), new Vector2Int(10, 5), out _));

            Assert.AreEqual(2, agent.StairsFloorsCrossedThisLeg);
            Assert.AreEqual(0f, agent.Stress);
        }

        [Test]
        public void Non_atrium_stairs_over_cap_still_adds_stress()
        {
            var grid = BuildGrid();
            var agent = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero);
            agent.Stress = 0f;
            agent.StairsFloorsCrossedThisLeg = 3;

            Assert.IsTrue(AgentSystem.TryApplyStairStep(
                agent, grid, new Vector2Int(8, 3), new Vector2Int(8, 4), out _));
            Assert.AreEqual(4, agent.StairsFloorsCrossedThisLeg);
            Assert.AreEqual(ElevatorRouting.StairsOverCapStressPerFloor, agent.Stress);
        }

        [Test]
        public void StairSpanMath_resets_at_transfer_floors()
        {
            var transfers = new List<int> { 0, 1, 2, 3 };

            Assert.AreEqual(4, StairSpanMath.MaxSegmentSpan(1, 5, new List<int>()));
            // Atrium top (3) + 2 floors: span 2, not atrium 3 + 2.
            Assert.AreEqual(2, StairSpanMath.MaxSegmentSpan(3, 5, transfers));
            Assert.AreEqual(2, StairSpanMath.MaxSegmentSpan(1, 5, transfers));
            Assert.AreEqual(2, StairSpanMath.MaxSegmentSpan(5, 1, transfers));
            Assert.AreEqual(0, StairSpanMath.MaxSegmentSpan(4, 4, transfers));
        }

        [Test]
        public void Router_treats_stairs_from_atrium_bottom_as_affordable_for_stressed_agent()
        {
            var grid = BuildGrid();
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);

            // Raw |dy| = 4 would be over comfort and unaffordable at stress 100;
            // atrium floors reset the chain so the longest segment is 2.
            Assert.IsTrue(router.TryPlanTrip(
                new Vector2Int(7, 1),
                new Vector2Int(10, 5),
                agentStress: 100f,
                agentRole: AgentRole.OfficeWorker,
                out var legs));
            Assert.AreEqual(1, legs.Count);
            Assert.AreEqual(TransitLegKind.Stairs, legs[0].Kind);
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

        static RoomTypeSO PadSo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "pad";
            so.category = RoomCategory.Office;
            so.size = Vector2Int.one;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO StairsSo()
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

        static RoomTypeSO AtriumSo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_atrium";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            so.size = new Vector2Int(8, 3);
            so.allowAboveGround = true;
            so.isAtrium = true;
            return so;
        }
    }
}
