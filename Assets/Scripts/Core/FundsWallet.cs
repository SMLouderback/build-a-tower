namespace BuildATower
{
    public sealed class FundsWallet
    {
        public int Balance { get; private set; }

        public FundsWallet(int startingBalance) => Balance = startingBalance;

        public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount)) return false;
            Balance -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount < 0) return;
            Balance += amount;
        }

        public void Subtract(int amount)
        {
            if (amount < 0) return;
            Balance = System.Math.Max(0, Balance - amount);
        }
    }
}
