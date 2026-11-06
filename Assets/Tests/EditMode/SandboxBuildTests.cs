using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class SandboxBuildTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        [TearDown]
        public void TearDown() => GameSession.ResetForTests();

        [Test]
        public void Sandbox_TrySpendForBuild_does_not_debit()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            var wallet = new FundsWallet(1000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(wallet, 500));
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void Normal_TrySpendForBuild_debits()
        {
            GameSession.StartNewGame(GameDifficulty.Normal);
            var wallet = new FundsWallet(1000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(wallet, 500));
            Assert.AreEqual(500, wallet.Balance);
        }

        [Test]
        public void Sandbox_CanAffordBuild_always_true()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            var wallet = new FundsWallet(0);
            Assert.IsTrue(BuildEconomy.CanAffordBuild(wallet, 99999));
        }

        [Test]
        public void Sandbox_RecordedSpend_is_zero()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            Assert.AreEqual(0, BuildEconomy.RecordedSpend(40000));
        }
    }
}
