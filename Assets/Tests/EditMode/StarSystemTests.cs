using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class StarSystemTests
    {
        static RoomTypeSO Lobby()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "lobby";
            type.isLobby = true;
            type.size = Vector2Int.one;
            return type;
        }

        static RoomTypeSO Elevator()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "elevator";
            type.isElevatorShaft = true;
            type.size = new Vector2Int(1, 2);
            type.allowAboveGround = true;
            type.requiredStars = 1;
            return type;
        }

        static TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 0, TowerGrid.LobbyFloor, out _));
            return grid;
        }

        [Test]
        public void Evaluate_grants_one_star_when_thresholds_met()
        {
            var stars = new StarSystem();

            stars.Evaluate(GridWithLobby(), averageStress: 10f, population: 10);

            Assert.AreEqual(1, stars.CurrentStars);
        }

        [Test]
        public void Evaluate_demotes_when_current_tier_fails()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            stars.Evaluate(grid, averageStress: 10f, population: 10);

            stars.Evaluate(grid, averageStress: 50f, population: 10);

            Assert.AreEqual(0, stars.CurrentStars);
        }

        [Test]
        public void Evaluate_promotes_to_two_stars_when_elevator_thresholds_met()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out _));
            stars.Evaluate(grid, averageStress: 10f, population: 10);

            stars.Evaluate(grid, averageStress: 25f, population: 30);

            Assert.AreEqual(2, stars.CurrentStars);
        }

        [Test]
        public void CanBuild_blocks_elevator_until_one_star()
        {
            var stars = new StarSystem();
            var elevator = Elevator();

            Assert.IsFalse(stars.CanBuild(elevator));

            stars.Evaluate(GridWithLobby(), averageStress: 10f, population: 10);

            Assert.IsTrue(stars.CanBuild(elevator));
        }
    }
}
