using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class FundsWalletTests
    {
        [Test]
        public void TrySpend_fails_when_insufficient()
        {
            var wallet = new FundsWallet(1000);
            Assert.IsFalse(wallet.TrySpend(1001));
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void TrySpend_succeeds_when_affordable()
        {
            var wallet = new FundsWallet(2_000_000);
            Assert.IsTrue(wallet.TrySpend(40_000));
            Assert.AreEqual(1_960_000, wallet.Balance);
        }

        [Test]
        public void Subtract_clamps_balance_at_zero()
        {
            var wallet = new FundsWallet(1_000);

            wallet.Subtract(1_001);

            Assert.AreEqual(0, wallet.Balance);
        }
    }
}
