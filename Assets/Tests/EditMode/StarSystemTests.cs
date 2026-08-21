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

        static RoomTypeSO Service(string id)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = id;
            type.size = Vector2Int.one;
            type.allowAboveGround = true;
            type.requiredStars = 2;
            return type;
        }

        static TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 0, TowerGrid.LobbyFloor, out _));
            return grid;
        }

        static TowerGrid GridReadyForTwoStars()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 5, TowerGrid.LobbyFloor, out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(0, 0), out _));
            return grid;
        }

        static bool PlaceServices(TowerGrid grid, out RoomInstance housekeeping)
        {
            housekeeping = null;
            if (!grid.TryPlace(Service("service_housekeeping"), new Vector2Int(2, 1), out housekeeping))
                return false;
            if (!grid.TryPlace(Service("service_maintenance"), new Vector2Int(3, 1), out _))
                return false;
            return true;
        }

        static TowerGrid GridReadyForThreeStars()
        {
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out _));
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
        public void TryPromote_cascades_from_zero_to_two_when_fully_qualified()
        {
            var stars = new StarSystem();

            Assert.IsTrue(stars.TryPromote(GridReadyForTwoStars(), averageStress: 10f, population: 30));

            Assert.AreEqual(2, stars.CurrentStars);
            StringAssert.Contains("2★", stars.LastResult);
        }

        [Test]
        public void TryPromote_cascades_enqueue_one_event_per_star()
        {
            var stars = new StarSystem();
            Assert.IsTrue(stars.TryPromote(GridReadyForTwoStars(), averageStress: 10f, population: 30));
            Assert.AreEqual(2, stars.CurrentStars);
            Assert.AreEqual(2, stars.PendingChanges.Count);
            Assert.AreEqual(StarChangeKind.Promoted, stars.PendingChanges[0].Kind);
            Assert.AreEqual(1, stars.PendingChanges[0].Stars);
            Assert.AreEqual(StarChangeKind.Promoted, stars.PendingChanges[1].Kind);
            Assert.AreEqual(2, stars.PendingChanges[1].Stars);
        }

        [Test]
        public void EvaluateQuarterly_demote_enqueues_demoted_event()
        {
            var stars = new StarSystem();
            stars.ForceStars(1);
            stars.ClearPendingChanges();
            // Lobby-only grid fails 1★ population/stress — use stress/pop that fail MeetsCriteria(1)
            stars.EvaluateQuarterly(GridWithLobby(), averageStress: 90f, population: 1);
            Assert.AreEqual(0, stars.CurrentStars);
            Assert.AreEqual(1, stars.PendingChanges.Count);
            Assert.AreEqual(StarChangeKind.Demoted, stars.PendingChanges[0].Kind);
            Assert.AreEqual(0, stars.PendingChanges[0].Stars);
        }

        [Test]
        public void ForceStars_does_not_enqueue_and_clears_pending()
        {
            var stars = new StarSystem();
            stars.TryPromote(GridReadyForTwoStars(), averageStress: 10f, population: 30);
            Assert.IsTrue(stars.PendingChanges.Count > 0);
            stars.ForceStars(5);
            Assert.AreEqual(0, stars.PendingChanges.Count);
            Assert.AreEqual(5, stars.CurrentStars);
        }

        [Test]
        public void ClearPendingChanges_empties_list()
        {
            var stars = new StarSystem();
            stars.TryPromote(GridWithLobby(), averageStress: 10f, population: 10);
            stars.ClearPendingChanges();
            Assert.AreEqual(0, stars.PendingChanges.Count);
        }

        [Test]
        public void TryPromote_does_not_skip_to_two_without_elevator()
        {
            var stars = new StarSystem();

            Assert.IsTrue(stars.TryPromote(GridWithLobby(), averageStress: 10f, population: 30));

            Assert.AreEqual(1, stars.CurrentStars);
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
        [TestCase(5, StarSystem.MaxStars)]
        public void ForceStars_clamps_requested_test_tier(int requestedStars, int expectedStars)
        {
            var stars = new StarSystem();

            stars.ForceStars(requestedStars);

            Assert.AreEqual(expectedStars, stars.CurrentStars);
        }

        [Test]
        public void MaxStars_is_five()
        {
            Assert.AreEqual(5, StarSystem.MaxStars);
            Assert.AreEqual(60, StarSystem.ThreeStarPopulation);
            Assert.AreEqual(20f, StarSystem.ThreeStarMaxStress);
            Assert.AreEqual(100, StarSystem.FourStarPopulation);
            Assert.AreEqual(15f, StarSystem.FourStarMaxStress);
            Assert.AreEqual(150, StarSystem.FiveStarPopulation);
            Assert.AreEqual(12f, StarSystem.FiveStarMaxStress);
        }

        [Test]
        public void TryPromote_reaches_three_stars_when_facilities_and_thresholds_met()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out _));
            stars.ForceStars(2);

            Assert.IsTrue(stars.TryPromote(grid, averageStress: 20f, population: 60));

            Assert.AreEqual(3, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_cascades_from_zero_to_three_when_fully_qualified()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out _));

            Assert.IsTrue(stars.TryPromote(grid, averageStress: 10f, population: 60));

            Assert.AreEqual(3, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_three_stars_without_all_facilities()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(grid.TryPlace(Service("service_housekeeping"), new Vector2Int(2, 1), out _));
            // Missing maintenance
            stars.ForceStars(2);

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 20f, population: 60));

            Assert.AreEqual(2, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_reaches_three_stars_without_security()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out _));
            stars.ForceStars(2);

            Assert.IsTrue(stars.TryPromote(grid, averageStress: 20f, population: 60));
            Assert.AreEqual(3, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_three_stars_when_housekeeping_is_broken()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out var housekeeping));
            housekeeping.Condition = 0;
            stars.ForceStars(2);

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 20f, population: 60));

            Assert.AreEqual(2, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_three_stars_when_population_or_stress_miss()
        {
            var stars = new StarSystem();
            var grid = GridReadyForTwoStars();
            Assert.IsTrue(PlaceServices(grid, out _));
            stars.ForceStars(2);

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 20f, population: 59));
            Assert.IsFalse(stars.TryPromote(grid, averageStress: 20.1f, population: 60));

            Assert.AreEqual(2, stars.CurrentStars);
        }

        [Test]
        public void FormatNextStarGoal_lists_facilities_for_three_star_tier()
        {
            var stars = new StarSystem();
            stars.ForceStars(2);

            var goal = stars.FormatNextStarGoal(GridReadyForTwoStars(), averageStress: 10f, population: 40);

            StringAssert.Contains("3★", goal);
            StringAssert.Contains($"Pop 40/{StarSystem.ThreeStarPopulation}", goal);
            StringAssert.Contains("Stress 10/20", goal);
            StringAssert.DoesNotContain("Security", goal);
            StringAssert.Contains("Housekeeping", goal);
            StringAssert.Contains("Maintenance", goal);
        }

        [Test]
        public void TryPromote_to_four_requires_staffed_security_and_thresholds()
        {
            var stars = new StarSystem();
            stars.ForceStars(3);
            var grid = GridReadyForThreeStars();

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 100));
            Assert.AreEqual(3, stars.CurrentStars);

            Assert.IsTrue(grid.TryPlace(Service("service_security"), new Vector2Int(1, 1), out var security));
            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 100));
            Assert.AreEqual(3, stars.CurrentStars);

            security.SetStaffedWorkers(1);
            Assert.IsTrue(stars.TryPromote(grid, averageStress: 10f, population: 100));
            Assert.AreEqual(4, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_four_stars_when_security_is_broken()
        {
            var stars = new StarSystem();
            stars.ForceStars(3);
            var grid = GridReadyForThreeStars();
            Assert.IsTrue(grid.TryPlace(Service("service_security"), new Vector2Int(1, 1), out var security));
            security.SetStaffedWorkers(1);
            security.Condition = 0;

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 100));
            Assert.AreEqual(3, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_four_stars_when_population_or_stress_miss()
        {
            var stars = new StarSystem();
            stars.ForceStars(3);
            var grid = GridReadyForThreeStars();
            Assert.IsTrue(grid.TryPlace(Service("service_security"), new Vector2Int(1, 1), out var security));
            security.SetStaffedWorkers(1);

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 99));
            Assert.IsFalse(stars.TryPromote(grid, averageStress: 15.1f, population: 100));
            Assert.AreEqual(3, stars.CurrentStars);
        }

        [Test]
        public void FormatNextStarGoal_lists_staffed_security_for_four_star_tier()
        {
            var stars = new StarSystem();
            stars.ForceStars(3);

            var goal = stars.FormatNextStarGoal(GridReadyForThreeStars(), averageStress: 10f, population: 80);

            StringAssert.Contains("4★", goal);
            StringAssert.Contains($"Pop 80/{StarSystem.FourStarPopulation}", goal);
            StringAssert.Contains("Stress 10/15", goal);
            StringAssert.Contains("Security", goal);
        }

        [Test]
        public void TryPromote_to_five_requires_valet_and_parking_stalls()
        {
            var stars = new StarSystem();
            stars.ForceStars(4);
            var grid = GridReadyForFourStars();

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 150));
            Assert.AreEqual(4, stars.CurrentStars);

            Assert.IsTrue(grid.TryPlace(Valet(), new Vector2Int(0, -1), out _));
            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 150));

            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(4, -1), out _));
            Assert.IsTrue(stars.TryPromote(grid, averageStress: 10f, population: 150));
            Assert.AreEqual(5, stars.CurrentStars);
        }

        [Test]
        public void TryPromote_blocks_five_when_population_or_stress_miss()
        {
            var stars = new StarSystem();
            stars.ForceStars(4);
            var grid = GridReadyForFiveStars();

            Assert.IsFalse(stars.TryPromote(grid, averageStress: 10f, population: 149));
            Assert.IsFalse(stars.TryPromote(grid, averageStress: 12.1f, population: 150));
            Assert.AreEqual(4, stars.CurrentStars);
        }

        [Test]
        public void FormatNextStarGoal_lists_valet_and_parking_for_five_star_tier()
        {
            var stars = new StarSystem();
            stars.ForceStars(4);

            var goal = stars.FormatNextStarGoal(GridReadyForFourStars(), averageStress: 10f, population: 120);

            StringAssert.Contains("5★", goal);
            StringAssert.Contains($"Pop 120/{StarSystem.FiveStarPopulation}", goal);
            StringAssert.Contains("Stress 10/12", goal);
            StringAssert.Contains("Valet", goal);
            StringAssert.Contains("Parking stalls", goal);
        }

        static TowerGrid GridReadyForFourStars()
        {
            var grid = GridReadyForThreeStars();
            Assert.IsTrue(grid.TryPlace(Service("service_security"), new Vector2Int(1, 1), out var security));
            security.SetStaffedWorkers(1);
            return grid;
        }

        static TowerGrid GridReadyForFiveStars()
        {
            var grid = GridReadyForFourStars();
            Assert.IsTrue(grid.TryPlace(Valet(), new Vector2Int(0, -1), out _));
            Assert.IsTrue(grid.TryPlace(Parking(), new Vector2Int(4, -1), out _));
            return grid;
        }

        static RoomTypeSO Valet()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = ParkingStalls.ValetId;
            type.category = RoomCategory.Service;
            type.size = new Vector2Int(3, 1);
            type.allowBasement = true;
            type.requiredStars = 4;
            return type;
        }

        static RoomTypeSO Parking()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = ParkingStalls.ParkingId;
            type.category = RoomCategory.Parking;
            type.size = new Vector2Int(6, 1);
            type.allowBasement = true;
            type.maxOccupants = 6;
            type.requiredStars = 4;
            return type;
        }
    }
}
