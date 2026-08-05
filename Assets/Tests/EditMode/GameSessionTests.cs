using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class GameSessionTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        [TearDown]
        public void TearDown() => GameSession.ResetForTests();

        [Test]
        public void EnsureDefault_sets_Normal_when_unset()
        {
            GameSession.EnsureDefault();
            Assert.AreEqual(GameDifficulty.Normal, GameSession.Difficulty);
            Assert.IsTrue(GameSession.HasDifficulty);
        }

        [Test]
        public void StartNewGame_sets_difficulty()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            Assert.AreEqual(GameDifficulty.Sandbox, GameSession.Difficulty);
        }

        [Test]
        public void EnsureDefault_does_not_overwrite_existing()
        {
            GameSession.StartNewGame(GameDifficulty.Hard);
            GameSession.EnsureDefault();
            Assert.AreEqual(GameDifficulty.Hard, GameSession.Difficulty);
        }
    }
}
