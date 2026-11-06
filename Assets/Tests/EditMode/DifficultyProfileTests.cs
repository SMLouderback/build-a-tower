using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class DifficultyProfileTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        [TearDown]
        public void TearDown() => GameSession.ResetForTests();

        [Test]
        public void StartingFunds_match_spec_table()
        {
            Assert.AreEqual(1_500_000, DifficultyProfile.StartingFunds(GameDifficulty.Easy));
            Assert.AreEqual(1_125_000, DifficultyProfile.StartingFunds(GameDifficulty.Normal));
            Assert.AreEqual(900_000, DifficultyProfile.StartingFunds(GameDifficulty.Hard));
            Assert.AreEqual(600_000, DifficultyProfile.StartingFunds(GameDifficulty.Extreme));
        }

        [Test]
        public void Hard_build_cost_exceeds_Normal()
        {
            GameSession.StartNewGame(GameDifficulty.Normal);
            var normal = BuildEconomy.EffectiveBuildCost(10_000);
            GameSession.StartNewGame(GameDifficulty.Hard);
            var hard = BuildEconomy.EffectiveBuildCost(10_000);
            Assert.AreEqual(10_000, normal);
            Assert.AreEqual(12_500, hard);
        }

        [Test]
        public void Easy_income_exceeds_Normal()
        {
            Assert.AreEqual(1000, DifficultyProfile.ApplyIncome(1000, GameDifficulty.Normal));
            Assert.AreEqual(1250, DifficultyProfile.ApplyIncome(1000, GameDifficulty.Easy));
            Assert.AreEqual(800, DifficultyProfile.ApplyIncome(1000, GameDifficulty.Hard));
            Assert.AreEqual(650, DifficultyProfile.ApplyIncome(1000, GameDifficulty.Extreme));
        }

        [Test]
        public void Sandbox_spend_is_free_Hard_debits_scaled()
        {
            GameSession.StartNewGame(GameDifficulty.Sandbox);
            var wallet = new FundsWallet(1000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(wallet, 500));
            Assert.AreEqual(1000, wallet.Balance);

            GameSession.StartNewGame(GameDifficulty.Hard);
            var hardWallet = new FundsWallet(20_000);
            Assert.IsTrue(BuildEconomy.TrySpendForBuild(hardWallet, 10_000));
            Assert.AreEqual(20_000 - 12_500, hardWallet.Balance);
        }
    }
}
