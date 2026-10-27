using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TutorialDirectorTests
    {
        TutorialDirector _director;
        readonly List<Object> _owned = new();

        [SetUp]
        public void SetUp()
        {
            _director = new TutorialDirector();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }

            _owned.Clear();
        }

        T Track<T>(T so) where T : Object
        {
            _owned.Add(so);
            return so;
        }

        RoomTypeSO Lobby()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO Office()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "office";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Condo()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "condo";
            so.category = RoomCategory.Condo;
            so.size = new Vector2Int(6, 1);
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Hotel()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "hotel";
            so.category = RoomCategory.Hotel;
            so.size = new Vector2Int(8, 1);
            so.allowAboveGround = true;
            return so;
        }

        RoomTypeSO Stairs()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "stairs";
            so.category = RoomCategory.Transit;
            so.isStairs = true;
            so.size = new Vector2Int(2, 2);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        RoomTypeSO Elevator()
        {
            var so = Track(ScriptableObject.CreateInstance<RoomTypeSO>());
            so.id = "elevator_normal";
            so.displayName = "Elevator";
            so.category = RoomCategory.Transit;
            so.size = new Vector2Int(1, 2);
            so.buildCost = 20000;
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
        }

        void BeginCoach()
        {
            _director.TryStart();
            _director.Continue();
        }

        [Test]
        public void Starts_inactive_and_not_active()
        {
            Assert.AreEqual(TutorialStep.Inactive, _director.CurrentStep);
            Assert.IsFalse(_director.IsActive);
        }

        [Test]
        public void TryStart_enters_Intro_AwaitingContinue()
        {
            _director.TryStart();
            Assert.AreEqual(TutorialStep.Intro, _director.CurrentStep);
            Assert.IsTrue(_director.IsActive);
            Assert.IsTrue(_director.AwaitingContinue);
            Assert.IsFalse(_director.CoachDocked);
        }

        [Test]
        public void Continue_from_Intro_enters_LobbyTool_and_docks()
        {
            _director.TryStart();
            _director.Continue();
            Assert.AreEqual(TutorialStep.LobbyTool, _director.CurrentStep);
            Assert.IsTrue(_director.IsActive);
            Assert.IsFalse(_director.AwaitingContinue);
            Assert.IsTrue(_director.CoachDocked);
        }

        [Test]
        public void Tick_while_Intro_does_not_advance()
        {
            _director.TryStart();
            var grid = new TowerGrid();
            _director.Tick(grid, 0, lobbyToolSelected: true);
            Assert.AreEqual(TutorialStep.Intro, _director.CurrentStep);
        }

        [Test]
        public void Skip_sets_Inactive_and_IsActive_false()
        {
            _director.TryStart();
            _director.Skip();
            Assert.AreEqual(TutorialStep.Inactive, _director.CurrentStep);
            Assert.IsFalse(_director.IsActive);
        }

        [Test]
        public void LobbyTool_completes_when_lobby_tool_selected()
        {
            BeginCoach();
            var grid = new TowerGrid();
            _director.Tick(grid, 0, lobbyToolSelected: true);
            Assert.AreEqual(TutorialStep.PlaceLobby, _director.CurrentStep);
        }

        [Test]
        public void Soft_skip_ahead_when_lobby_already_exists()
        {
            BeginCoach();
            var grid = GridWithLobby();
            _director.Tick(grid, 0, lobbyToolSelected: false);
            Assert.AreEqual(TutorialStep.PlaceLiving, _director.CurrentStep);
            Assert.IsTrue(_director.IsActive);
        }

        [Test]
        public void PlaceLobby_completes_on_HasLobby()
        {
            BeginCoach();
            var grid = new TowerGrid();
            _director.Tick(grid, 0, lobbyToolSelected: true);
            Assert.AreEqual(TutorialStep.PlaceLobby, _director.CurrentStep);

            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.PlaceLiving, _director.CurrentStep);
        }

        [Test]
        public void PlaceLiving_completes_for_Office_Condo_or_Hotel(
            [Values(RoomCategory.Office, RoomCategory.Condo, RoomCategory.Hotel)] RoomCategory category)
        {
            BeginCoach();
            var grid = GridWithLobby();
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.PlaceLiving, _director.CurrentStep);

            RoomTypeSO living = category switch
            {
                RoomCategory.Office => Office(),
                RoomCategory.Condo => Condo(),
                _ => Hotel()
            };
            Assert.IsTrue(grid.TryPlace(living, new Vector2Int(0, 1), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.PlaceStairs, _director.CurrentStep);
        }

        [Test]
        public void PlaceStairs_completes_when_stairs_exist()
        {
            BeginCoach();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.PlaceStairs, _director.CurrentStep);

            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.WaitElevator, _director.CurrentStep);
        }

        [Test]
        public void WaitElevator_completes_when_stars_at_least_one()
        {
            BeginCoach();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialStep.WaitElevator, _director.CurrentStep);

            _director.Tick(grid, 1, false);
            Assert.AreEqual(TutorialStep.PlaceElevator, _director.CurrentStep);
        }

        [Test]
        public void WaitElevator_completes_when_elevator_already_exists()
        {
            BeginCoach();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out _));
            _director.Tick(grid, 0, false);
            // Wait + Place + Extend (span>=2) soft-skip to Done
            Assert.AreEqual(TutorialStep.Done, _director.CurrentStep);
        }

        [Test]
        public void PlaceElevator_then_ExtendElevator_soft_completes_when_span_at_least_two()
        {
            BeginCoach();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            _director.Tick(grid, 1, false);
            Assert.AreEqual(TutorialStep.PlaceElevator, _director.CurrentStep);
            Assert.AreEqual(TutorialHighlightTarget.ElevatorTool, _director.GetHighlight().Target);

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out _));
            _director.Tick(grid, 1, false);
            Assert.AreEqual(TutorialStep.Done, _director.CurrentStep);
            Assert.IsFalse(_director.IsActive);
        }

        [Test]
        public void ExtendElevator_completes_when_span_grows_beyond_snapshot()
        {
            // Snapshot taken on entering PlaceElevator while a shaft already exists (span 2).
            // Soft-skip may finish immediately via span>=2; growth path still reaches Done.
            BeginCoach();
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out var shaft));

            _director.Tick(grid, 1, false);
            if (_director.CurrentStep != TutorialStep.Done)
            {
                Assert.IsTrue(grid.TryExtendElevator(shaft, 0, 4, out _));
                _director.Tick(grid, 1, false);
            }

            Assert.AreEqual(TutorialStep.Done, _director.CurrentStep);
            Assert.IsFalse(_director.IsActive);
        }

        [Test]
        public void GetHighlight_maps_targets_for_coaching_steps()
        {
            _director.TryStart();
            var intro = _director.GetHighlight();
            Assert.AreEqual(TutorialHighlightTarget.None, intro.Target);
            Assert.IsFalse(string.IsNullOrEmpty(intro.Title));
            Assert.IsFalse(string.IsNullOrEmpty(intro.Body));

            BeginCoach();
            var h = _director.GetHighlight();
            Assert.AreEqual(TutorialHighlightTarget.LobbyTool, h.Target);
            Assert.IsFalse(string.IsNullOrEmpty(h.Title));
            Assert.IsFalse(string.IsNullOrEmpty(h.Body));

            var grid = new TowerGrid();
            _director.Tick(grid, 0, lobbyToolSelected: true);
            Assert.AreEqual(TutorialHighlightTarget.LobbyFloorBand, _director.GetHighlight().Target);

            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialHighlightTarget.LivingFamily, _director.GetHighlight().Target);

            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialHighlightTarget.StairsTool, _director.GetHighlight().Target);

            Assert.IsTrue(grid.TryPlace(Stairs(), new Vector2Int(10, 0), out _));
            _director.Tick(grid, 0, false);
            Assert.AreEqual(TutorialHighlightTarget.ElevatorTool, _director.GetHighlight().Target);

            _director.Tick(grid, 1, false);
            Assert.AreEqual(TutorialHighlightTarget.ElevatorTool, _director.GetHighlight().Target);

            Assert.IsTrue(grid.TryPlace(Elevator(), new Vector2Int(5, 0), out _));
            _director.Tick(grid, 1, false);
            Assert.AreEqual(TutorialStep.Done, _director.CurrentStep);
            Assert.AreEqual(TutorialHighlightTarget.None, _director.GetHighlight().Target);
        }

        [Test]
        public void Tick_while_Inactive_or_after_Skip_is_noop()
        {
            var grid = GridWithLobby();
            _director.Tick(grid, 5, true);
            Assert.AreEqual(TutorialStep.Inactive, _director.CurrentStep);

            _director.TryStart();
            _director.Skip();
            _director.Tick(grid, 5, true);
            Assert.AreEqual(TutorialStep.Inactive, _director.CurrentStep);
        }

        [Test]
        public void TryStart_is_idempotent_while_already_active()
        {
            BeginCoach();
            var grid = new TowerGrid();
            _director.Tick(grid, 0, lobbyToolSelected: true);
            Assert.AreEqual(TutorialStep.PlaceLobby, _director.CurrentStep);

            _director.TryStart();
            Assert.AreEqual(TutorialStep.PlaceLobby, _director.CurrentStep);
        }

        [Test]
        public void Skip_does_not_change_TutorialPrefs()
        {
            TutorialPrefs.ResetForTests();
            Assert.IsTrue(TutorialPrefs.GuidedTutorialEnabled);
            _director.TryStart();
            _director.Skip();
            Assert.IsTrue(TutorialPrefs.GuidedTutorialEnabled);
            TutorialPrefs.ResetForTests();
        }
    }
}
