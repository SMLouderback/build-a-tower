using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ResearchHudPanelTests
    {
        [Test]
        public void CountResearchLabs_empty_grid_is_zero()
        {
            var grid = NewGridWithLobby();
            Assert.AreEqual(0, EconomySystem.CountResearchLabs(grid));
            Assert.AreEqual(0, EconomySystem.CountNonBrokenResearchLabs(grid));
        }

        [Test]
        public void CountResearchLabs_one_healthy_lab()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            Assert.IsFalse(lab.IsBroken);

            Assert.AreEqual(1, EconomySystem.CountResearchLabs(grid));
            Assert.AreEqual(1, EconomySystem.CountNonBrokenResearchLabs(grid));
        }

        [Test]
        public void CountResearchLabs_one_broken_lab_still_counts()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var lab));
            lab.Condition = 0;
            Assert.IsTrue(lab.IsBroken);

            Assert.AreEqual(1, EconomySystem.CountResearchLabs(grid));
            Assert.AreEqual(0, EconomySystem.CountNonBrokenResearchLabs(grid));
        }

        [Test]
        public void CountResearchLabs_mixed_healthy_and_broken()
        {
            var grid = NewGridWithLobby();
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(0, 1), out var healthy));
            Assert.IsTrue(grid.TryPlace(ResearchLab(), new Vector2Int(4, 1), out var broken));
            broken.Condition = 0;
            Assert.IsFalse(healthy.IsBroken);
            Assert.IsTrue(broken.IsBroken);

            Assert.AreEqual(2, EconomySystem.CountResearchLabs(grid));
            Assert.AreEqual(1, EconomySystem.CountNonBrokenResearchLabs(grid));
        }

        [Test]
        public void StatusCaption_idle_when_null_or_not_running()
        {
            Assert.AreEqual("Research · Idle", ResearchHudPanel.StatusCaption(null));

            var research = new ResearchSystem();
            Assert.IsFalse(research.IsRunning);
            Assert.AreEqual("Research · Idle", ResearchHudPanel.StatusCaption(research));
        }

        [Test]
        public void StatusCaption_running_shows_branch_and_level()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Marketing, 1));
            Assert.IsTrue(research.IsRunning);
            Assert.IsFalse(research.IsPaused);

            Assert.AreEqual(
                $"Research · {ResearchCatalog.BranchDisplayName(ResearchBranch.Marketing)} L1",
                ResearchHudPanel.StatusCaption(research));
        }

        [Test]
        public void StatusCaption_paused_while_running()
        {
            var research = new ResearchSystem();
            Assert.IsTrue(research.TryStart(ResearchBranch.Elevator, 1));
            research.Pause();
            Assert.IsTrue(research.IsRunning);
            Assert.IsTrue(research.IsPaused);

            Assert.AreEqual("Research · Paused", ResearchHudPanel.StatusCaption(research));
        }

        static TowerGrid NewGridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
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

        static RoomTypeSO ResearchLab()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = EconomySystem.ResearchId;
            so.size = new Vector2Int(4, 1);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }
    }
}
