using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class TutorialBootTests
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

        [Test]
        public void TryStartFromSession_when_flag_set_starts_director_and_consumes()
        {
            TutorialPrefs.GuidedTutorialEnabled = true;
            GameSession.StartNewGame(GameDifficulty.Easy);
            var director = new TutorialDirector();

            Assert.IsTrue(TutorialBoot.TryStartFromSession(director));
            Assert.AreEqual(TutorialStep.Intro, director.CurrentStep);
            Assert.IsTrue(director.IsActive);
            Assert.IsTrue(director.AwaitingContinue);
            Assert.IsFalse(GameSession.TutorialMayStart);
            Assert.IsFalse(TutorialBoot.TryStartFromSession(director));
            Assert.AreEqual(TutorialStep.Intro, director.CurrentStep);
        }

        [Test]
        public void TryStartFromSession_when_flag_clear_does_not_start()
        {
            var director = new TutorialDirector();

            Assert.IsFalse(TutorialBoot.TryStartFromSession(director));
            Assert.AreEqual(TutorialStep.Inactive, director.CurrentStep);
            Assert.IsFalse(director.IsActive);
        }

        [Test]
        public void TryStartFromSession_null_director_returns_false_without_throwing()
        {
            TutorialPrefs.GuidedTutorialEnabled = true;
            GameSession.StartNewGame(GameDifficulty.Normal);

            Assert.IsFalse(TutorialBoot.TryStartFromSession(null));
            Assert.IsTrue(GameSession.TutorialMayStart);
        }

        [Test]
        public void IsLobbyToolSelected_null_build_is_false()
        {
            Assert.IsFalse(TutorialBoot.IsLobbyToolSelected(null));
        }
    }
}
