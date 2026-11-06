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
            if (amount > 0)
                GameSession.MarkGameplaySaveDirty();
            return true;
        }

        public void Add(int amount)
        {
            if (amount < 0) return;
            Balance += amount;
            if (amount > 0)
                GameSession.MarkGameplaySaveDirty();
        }

        public void Subtract(int amount)
        {
            if (amount < 0) return;
            if (amount == 0) return;
            Balance = System.Math.Max(0, Balance - amount);
            GameSession.MarkGameplaySaveDirty();
        }

        public void RestoreBalance(int balance)
        {
            if (balance < 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(balance),
                    balance,
                    "A restored wallet balance cannot be negative.");

            Balance = balance;
        }
    }
}
