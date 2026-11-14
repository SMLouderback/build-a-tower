using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class TutorialStartFlagTests
    {
        [SetUp]
        public void SetUp()
        {
            GameSession.ResetForTests();
            TutorialPrefs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetForTests();
            TutorialPrefs.ResetForTests();
        }

        [TestCase(GameDifficulty.Sandbox)]
        [TestCase(GameDifficulty.Easy)]
        [TestCase(GameDifficulty.Normal)]
        public void StartNewGame_sets_TutorialMayStart_when_guided_enabled(GameDifficulty difficulty)
        {
            TutorialPrefs.GuidedTutorialEnabled = true;

            GameSession.StartNewGame(difficulty);

            Assert.IsTrue(GameSession.TutorialMayStart);
        }

        [TestCase(GameDifficulty.Sandbox)]
        [TestCase(GameDifficulty.Easy)]
        [TestCase(GameDifficulty.Normal)]
        public void StartNewGame_clears_TutorialMayStart_when_guided_disabled(GameDifficulty difficulty)
        {
            TutorialPrefs.GuidedTutorialEnabled = false;

            GameSession.StartNewGame(difficulty);

            Assert.IsFalse(GameSession.TutorialMayStart);
        }

        [TestCase(GameDifficulty.Hard)]
        [TestCase(GameDifficulty.Extreme)]
        public void StartNewGame_Hard_or_Extreme_never_sets_TutorialMayStart(GameDifficulty difficulty)
        {
            TutorialPrefs.GuidedTutorialEnabled = true;

            GameSession.StartNewGame(difficulty);

            Assert.IsFalse(GameSession.TutorialMayStart);
        }

        [Test]
        public void PrepareLoad_clears_TutorialMayStart()
        {
            TutorialPrefs.GuidedTutorialEnabled = true;
            GameSession.StartNewGame(GameDifficulty.Easy);
            Assert.IsTrue(GameSession.TutorialMayStart);

            var result = GameSession.PrepareLoad(ValidSnapshot(nameof(GameDifficulty.Hard)));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.IsFalse(GameSession.TutorialMayStart);
        }

        [Test]
        public void ConsumeTutorialMayStart_returns_current_and_clears()
        {
            TutorialPrefs.GuidedTutorialEnabled = true;
            GameSession.StartNewGame(GameDifficulty.Normal);
            Assert.IsTrue(GameSession.TutorialMayStart);

            Assert.IsTrue(GameSession.ConsumeTutorialMayStart());
            Assert.IsFalse(GameSession.TutorialMayStart);
            Assert.IsFalse(GameSession.ConsumeTutorialMayStart());
        }

        static TowerSnapshotV1 ValidSnapshot(string difficulty) => new TowerSnapshotV1
        {
            schemaVersion = SaveSchema.CurrentVersion,
            saveId = "tutorial-flag-save",
            towerName = "Tutorial Flag Tower",
            difficulty = difficulty,
            walletBalance = 1000,
            stars = 0,
            rooms = System.Array.Empty<RoomSnapshotV1>(),
        };
    }
}
