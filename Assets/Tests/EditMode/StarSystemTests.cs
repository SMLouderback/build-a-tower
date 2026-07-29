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
        public void TryPromote_grants_one_star_when_thresholds_met()
        {
            var stars = new StarSystem();

            Assert.IsTrue(stars.TryPromote(GridWithLobby(), averageStress: 10f, population: 10));

            Assert.AreEqual(1, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_never_demotes_when_current_tier_fails()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            stars.TryPromote(grid, averageStress: 10f, population: 10);

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 90f, population: 1));

            Assert.AreEqual(1, stars.CurrentStars);
        }

        [Test]
        public void EvaluateQuarterly_demotes_when_current_tier_fails()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            stars.TryPromote(grid, averageStress: 10f, population: 10);

            stars.EvaluateQuarterly(grid, averageStress: 50f, population: 10);

            Assert.AreEqual(0, stars.CurrentStars);
        }

        [Test]
        public void EvaluateQuarterly_keeps_tier_when_criteria_still_met()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            stars.TryPromote(grid, averageStress: 10f, population: 10);

            stars.EvaluateQuarterly(grid, averageStress: 10f, population: 10);

            Assert.AreEqual(1, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_reaches_two_stars_when_elevator_thresholds_met()
        {
            var stars = new StarSystem();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out _));
            stars.TryPromote(grid, averageStress: 10f, population: 10);

            stars.TryPromote(grid, averageStress: 25f, population: 30);

            Assert.AreEqual(2, stars.CurrentStars);
        }

        [Test]
        public void CanBuild_blocks_elevator_until_one_star()
        {
            var stars = new StarSystem();
            var elevator = Elevator();

            Assert.IsFalse(stars.CanBuild(elevator));

            stars.TryPromote(GridWithLobby(), averageStress: 10f, population: 10);

            Assert.IsTrue(stars.CanBuild(elevator));
        }

        [Test]
        public void FormatNextStarGoal_shows_progress_toward_next_tier()
        {
            var stars = new StarSystem();

            var goal = stars.FormatNextStarGoal(GridWithLobby(), averageStress: 12f, population: 4);

            StringAssert.Contains("1★", goal);
            StringAssert.Contains($"Pop 4/{StarSystem.OneStarPopulation}", goal);
            StringAssert.Contains("Stress 12/40", goal);
        }

        [Test]
        public void FormatNextStarGoal_lists_elevator_for_two_star_tier()
        {
            var stars = new StarSystem();
            stars.ForceStars(1);

            var goal = stars.FormatNextStarGoal(GridWithLobby(), averageStress: 10f, population: 10);

            StringAssert.Contains("Elevator", goal);
        }

        [Test]
        public void FormatNextStarGoal_reports_max_tier()
        {
            var stars = new StarSystem();
            stars.ForceStars(StarSystem.MaxStars);

            StringAssert.Contains(
                "max tier",
                stars.FormatNextStarGoal(GridWithLobby(), averageStress: 0f, population: 100));
        }

        [TestCase(-1, 0)]
        [TestCase(1, 1)]
        [TestCase(3, StarSystem.MaxStars)]
        public void ForceStars_clamps_requested_test_tier(int requestedStars, int expectedStars)
        {
            var stars = new StarSystem();

            stars.ForceStars(requestedStars);

            Assert.AreEqual(expectedStars, stars.CurrentStars);
        }
    }
}
